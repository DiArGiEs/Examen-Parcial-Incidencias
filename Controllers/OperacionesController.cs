using Algolia.Search.Clients;
using Algolia.Search.Models.Search;
using Examen_Parcial_Incidencias.Data;
using Examen_Parcial_Incidencias.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Examen_Parcial_Incidencias.Controllers
{
    public class OperacionesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public OperacionesController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<IActionResult> Incidencias(string? q)
        {
            var query = _context.Incidencias.Where(i => i.Estado == "Abierta");

            if (!string.IsNullOrWhiteSpace(q))
            {
                try
                {
                    var appId = _configuration["Algolia:AppId"];
                    var apiKey = _configuration["Algolia:ApiKey"];
                    var indexName = _configuration["Algolia:IndexName"] ?? "incidencias";

                    if (!string.IsNullOrEmpty(appId) && !string.IsNullOrEmpty(apiKey))
                    {
                        var client = new SearchClient(appId, apiKey);
                        var response = await client.SearchSingleIndexAsync<Incidencia>(indexName, q);
                        var idsAlgolia = response.Hits.Select(h => h.Id).ToList();
                        query = query.Where(i => idsAlgolia.Contains(i.Id));
                    }
                    else
                    {
                        query = query.Where(i => i.Estacion.Contains(q) || i.Descripcion.Contains(q));
                    }
                }
                catch
                {
                    query = query.Where(i => i.Estacion.Contains(q) || i.Descripcion.Contains(q));
                }
            }

            ViewData["CurrentFilter"] = q;
            var result = await query.ToListAsync();
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
            }
            return RedirectToAction(nameof(Incidencias));
        }
    }
}