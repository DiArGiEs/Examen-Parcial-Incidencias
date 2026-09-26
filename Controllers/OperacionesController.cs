using System.Text.Json;
using Examen_Parcial_Incidencias.Data;
using Examen_Parcial_Incidencias.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace Examen_Parcial_Incidencias.Controllers
{
    public class OperacionesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IDistributedCache _cache;
        private readonly ILogger<OperacionesController> _logger;
        private const string CacheKey = "ListadoIncidenciasAbiertas";

        public OperacionesController(ApplicationDbContext context, IDistributedCache cache, ILogger<OperacionesController> logger)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
        }

        public async Task<IActionResult> Incidencias(string? q)
        {
            List<Incidencia>? result = null;

            if (!string.IsNullOrWhiteSpace(q))
            {
                _logger.LogInformation("Consulta con filtro 'q': omitiendo caché y consultando BD.");
                result = await _context.Incidencias
                    .Where(i => i.Estado == "Abierta" && (i.Estacion.Contains(q) || i.Descripcion.Contains(q)))
                    .ToListAsync();
            }
            else
            {
                string? cachedData = null;
                try
                {
                    cachedData = await _cache.GetStringAsync(CacheKey);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Redis no disponible en desarrollo local: {Message}", ex.Message);
                }

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _logger.LogInformation("HIT: Lectura realizada directamente desde REDIS.");
                    result = JsonSerializer.Deserialize<List<Incidencia>>(cachedData);
                }
                else
                {
                    _logger.LogInformation("MISS: Lectura realizada desde la BASE DE DATOS.");
                    result = await _context.Incidencias
                        .Where(i => i.Estado == "Abierta")
                        .ToListAsync();

                    try
                    {
                        var options = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60)
                        };
                        var serialized = JsonSerializer.Serialize(result);
                        await _cache.SetStringAsync(CacheKey, serialized, options);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("No se pudo guardar la clave en Redis: {Message}", ex.Message);
                    }
                }
            }

            ViewData["CurrentFilter"] = q;
            return View(result);
        }

        [HttpPost]
        public async Task<IActionResult> Cerrar(int id)
        {
            var incidencia = await _context.Incidencias.FindAsync(id);
            if (incidencia != null)
            {
                incidencia.Estado = "Cerrada";
                await _context.SaveChangesAsync();

                try
                {
                    await _cache.RemoveAsync(CacheKey);
                    _logger.LogInformation("INVALIDACIÓN: Clave {CacheKey} eliminada de Redis.", CacheKey);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("No se pudo invalidar Redis: {Message}", ex.Message);
                }
            }
            return RedirectToAction(nameof(Incidencias));
        }
    }
}