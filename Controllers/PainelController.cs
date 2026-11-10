using Microsoft.AspNetCore.Mvc;
using TalentDesk.Models;
using TalentDesk.Repositories;
using TalentDesk.Services;

namespace TalentDesk.Controllers
{
    // Demais itens do menu lateral
    public class PainelController : Controller
    {
        private readonly RelatorioService _relatorioService = new(new CurriculoRepository());

        [HttpGet]
        public IActionResult Relatorios([FromQuery] FiltroRelatorio filtro)
        {
            return View(_relatorioService.Montar(filtro));
        }

        [HttpGet]
        public IActionResult Vagas()
        {
            ViewData["Menu"] = "vagas";
            ViewData["Title"] = "Vagas";
            ViewData["Descricao"] = "Em breve você poderá cadastrar vagas e vincular currículos a cada processo seletivo.";
            return View("EmBreve");
        }

        // Endereço antigo: a aba agora tem controller próprio
        [HttpGet]
        public IActionResult Configuracoes()
        {
            return RedirectToAction("Index", "Configuracoes");
        }
    }
}
