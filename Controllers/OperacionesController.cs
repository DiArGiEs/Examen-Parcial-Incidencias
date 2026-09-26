using Examen_Parcial_Incidencias.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Examen_Parcial_Incidencias.Controllers
{
    public class OperacionesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public OperacionesController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Incidencias()
        {
            var abiertas = await _context.Incidencias
                .Where(i => i.Estado == "Abierta")
                .ToListAsync();
            return View(abiertas);
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