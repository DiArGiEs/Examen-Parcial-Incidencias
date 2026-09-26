using System.Text;
using System.Text.Json;
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
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<OperacionesController> _logger;

        public OperacionesController(
            ApplicationDbContext context,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            ILogger<OperacionesController> logger)
        {
            _context = context;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<IActionResult> Incidencias(string? q)
        {
            var query = _context.Incidencias.Where(i => i.Estado == "Abierta");

            if (!string.IsNullOrWhiteSpace(q))
            {
                query = query.Where(i => i.Estacion.Contains(q) || i.Descripcion.Contains(q));
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