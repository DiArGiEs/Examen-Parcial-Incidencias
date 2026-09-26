using Algolia.Search.Clients;
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
                    var appId = _configuration["Algolia:AppId"] ?? "DUMMY_APP_ID";
                    var apiKey = _configuration["Algolia:ApiKey"] ?? "DUMMY_SEARCH_KEY";
                    var indexName = _configuration["Algolia:IndexName"] ?? "incidencias";

                    var client = new SearchClient(appId, apiKey);
                    var index = client.InitIndex(indexName);
                    var searchResult = await index.SearchAsync<Incidencia>(new Algolia.Search.Models.Search.Query(q));

                    var idsAlgolia = searchResult.Hits.Select(h => h.Id).ToList();
                    query = query.Where(i => idsAlgolia.Contains(i.Id));
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