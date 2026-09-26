using Examen_Parcial_Incidencias.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=app.db";
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddControllersWithViews();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Operaciones}/{action=Incidencias}/{id?}");
app.MapRazorPages();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.EnsureCreated();
    if (!db.Incidencias.Any())
    {
        db.Incidencias.AddRange(
            new Examen_Parcial_Incidencias.Models.Incidencia { Estacion = "Estación Central", Descripcion = "Freno trasero defectuoso", Prioridad = "Alta", Estado = "Abierta" },
            new Examen_Parcial_Incidencias.Models.Incidencia { Estacion = "Estación Miraflores", Descripcion = "Cadena suelta en bicicleta #42", Prioridad = "Media", Estado = "Abierta" },
            new Examen_Parcial_Incidencias.Models.Incidencia { Estacion = "Estación San Isidro", Descripcion = "Luz delantera rota", Prioridad = "Baja", Estado = "Cerrada" }
        );
        db.SaveChanges();
    }
}

app.Run();