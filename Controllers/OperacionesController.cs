using System.Text;
using System.Text.Json;
using Algolia.Search.Clients;
using Algolia.Search.Models.Search;
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
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<OperacionesController> _logger;
        private const string CacheKey = "ListadoIncidenciasAbiertas";

        public OperacionesController(
            ApplicationDbContext context,
            IDistributedCache cache,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            ILogger<OperacionesController> logger)
        {
            _context = context;
            _cache = cache;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<IActionResult> Incidencias(string? q)
        {
            List<Incidencia>? result = null;

            if (!string.IsNullOrWhiteSpace(q))
            {
                _logger.LogInformation("Consulta con filtro 'q': buscando en Algolia/BD sin consultar caché.");
                try
                {
                    var appId = _configuration["Algolia:AppId"];
                    var apiKey = _configuration["Algolia:ApiKey"];
                    var indexName = _configuration["Algolia:IndexName"] ?? "incidencias";

                    if (!string.IsNullOrEmpty(appId) && !string.IsNullOrEmpty(apiKey))
                    {
                        var client = new SearchClient(appId, apiKey);
                        var searchParams = new SearchParams(new SearchParamsObject { Query = q });
                        var response = await client.SearchSingleIndexAsync<Incidencia>(indexName, searchParams);
                        var idsAlgolia = response.Hits.Select(h => h.Id).ToList();

                        result = await _context.Incidencias
                            .Where(i => i.Estado == "Abierta" && idsAlgolia.Contains(i.Id))
                            .ToListAsync();
                    }
                    else
                    {
                        result = await _context.Incidencias
                            .Where(i => i.Estado == "Abierta" && (i.Estacion.Contains(q) || i.Descripcion.Contains(q)))
                            .ToListAsync();
                    }
                }
                catch
                {
                    result = await _context.Incidencias
                        .Where(i => i.Estado == "Abierta" && (i.Estacion.Contains(q) || i.Descripcion.Contains(q)))
                        .ToListAsync();
                }
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
                    _logger.LogWarning("Redis no disponible localmente: {Message}", ex.Message);
                }

                if (!string.IsNullOrEmpty(cachedData))
                {
                    _logger.LogInformation("HIT: Lectura desde REDIS.");
                    result = JsonSerializer.Deserialize<List<Incidencia>>(cachedData);
                }
                else
                {
                    _logger.LogInformation("MISS: Lectura desde la BASE DE DATOS.");
                    result = await _context.Incidencias.Where(i => i.Estado == "Abierta").ToListAsync();

                    try
                    {
                        var options = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60)
                        };
                        await _cache.SetStringAsync(CacheKey, JsonSerializer.Serialize(result), options);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Error al guardar en Redis: {Message}", ex.Message);
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
                    _logger.LogInformation("INVALIDACIÓN: Clave de Redis eliminada tras cerrar incidencia.");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Error al invalidar Redis: {Message}", ex.Message);
                }

                try
                {
                    var pieHostEndpoint = _configuration["PieHost:PublishUrl"] ?? "https://pubsub.piehost.com/publish";
                    var apiKey = _configuration["PieHost:ApiKey"] ?? "DUMMY_KEY";

                    var payload = new
                    {
                        event_name = "IncidenciaActualizada",
                        data = new { id = incidencia.Id, estado = incidencia.Estado }
                    };

                    var client = _httpClientFactory.CreateClient();
                    var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                    client.DefaultRequestHeaders.Add("X-API-Key", apiKey);

                    var response = await client.PostAsync(pieHostEndpoint, content);
                    _logger.LogInformation("PieHost publicado con código: {StatusCode}", response.StatusCode);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al publicar evento en PieHost.");
                }
            }
            return RedirectToAction(nameof(Incidencias));
        }
    }
}