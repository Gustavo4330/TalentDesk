using Microsoft.AspNetCore.Mvc;
using TalentDesk.Filters;
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
        [Acesso(NivelAcesso.Empresa)]
        public IActionResult Relatorios([FromQuery] FiltroRelatorio filtro)
        {
            return View(_relatorioService.Montar(filtro));
        }

        // Endereço antigo: vagas agora têm controller próprio
        [HttpGet]
        public IActionResult Vagas()
        {
            return RedirectToAction("Index", "Vagas");
        }

        // Endereço antigo: a aba agora tem controller próprio
        [HttpGet]
        public IActionResult Configuracoes()
        {
            return RedirectToAction("Index", "Configuracoes");
        }
    }
}
