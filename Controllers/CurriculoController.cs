using Microsoft.AspNetCore.Mvc;
using TalentDesk.Models;

namespace TalentDesk.Controllers
{
    public class CurriculoController : Controller
    {
        private static List<Curriculo> curriculos = new()
        {
            new Curriculo
            {
                Id = 1,
                Nome = "Amanda Bonfim",
                Area = "RH",
                Formacao = "Psicologia",
                Idade = 28,
                FotoUrl = "https://i.pravatar.cc/150?img=32"
            },

            new Curriculo
            {
                Id = 2,
                Nome = "Felipe Castro",
                Area = "TI",
                Formacao = "Ciência da Computação",
                Idade = 25,
                FotoUrl = "https://i.pravatar.cc/150?img=12"
            }
        };

        public IActionResult Index()
        {
            return View(curriculos);
        }

        public IActionResult Detalhes(int id)
        {
            var curriculo = curriculos.FirstOrDefault(x => x.Id == id);

            if (curriculo == null)
            {
                return NotFound();
            }

            return View(curriculo);
        }
    }
}