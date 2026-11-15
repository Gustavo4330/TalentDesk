using Microsoft.AspNetCore.Mvc;
using TalentDesk.Filters;
using TalentDesk.Models;
using TalentDesk.Repositories;
using TalentDesk.Services;

namespace TalentDesk.Controllers
{
    public class CurriculoController : Controller
    {
        private readonly UsuarioRepository _usuarioRepository;
        private readonly CurriculoRepository _curriculoRepository;
        private readonly FavoritoService _favoritoService;

        public CurriculoController()
        {
            _usuarioRepository = new UsuarioRepository();
            _curriculoRepository = new CurriculoRepository();
            _favoritoService = new FavoritoService();
        }

        // ============ DASHBOARD / LISTA / FAVORITOS (empresa) ============

        [HttpGet]
        [Acesso(NivelAcesso.Empresa)]
        public IActionResult Index([FromQuery] FiltroCurriculo filtro)
        {
            // Valores inválidos na URL (ex.: ?pagina=abc) são ignorados
            if (!ModelState.IsValid)
            {
                if (ModelState[nameof(FiltroCurriculo.Pagina)]?.Errors.Count > 0) filtro.Pagina = 1;
                if (ModelState[nameof(FiltroCurriculo.Itens)]?.Errors.Count > 0) filtro.Itens = 5;
                if (ModelState[nameof(FiltroCurriculo.Selecionado)]?.Errors.Count > 0) filtro.Selecionado = null;
            }

            filtro.Normalizar();

            var favoritos = _favoritoService.ObterFavoritos(Request);
            var (curriculos, total) = _curriculoRepository.Buscar(filtro, favoritos);

            // Painel da direita: o currículo escolhido ou o primeiro da lista.
            // selecionado=0 significa painel fechado.
            Curriculo? selecionado = null;
            if (filtro.Selecionado is > 0)
            {
                selecionado = _curriculoRepository.ObterPorId(filtro.Selecionado.Value);
            }
            else if (filtro.Selecionado == null && curriculos.Count > 0)
            {
                selecionado = _curriculoRepository.ObterPorId(curriculos[0].Id);
            }

            var viewModel = new CurriculoIndexViewModel
            {
                Filtro = filtro,
                Curriculos = curriculos,
                TotalEncontrado = total,
                Selecionado = selecionado,
                Estatisticas = _curriculoRepository.ObterEstatisticas(favoritos),
                Areas = _curriculoRepository.ListarAreas(),
                Formacoes = _curriculoRepository.ListarFormacoes(),
                Cidades = _curriculoRepository.ListarCidades(),
                FavoritosIds = favoritos
            };

            return View(viewModel);
        }

        // ============ CURRÍCULO DO CANDIDATO ============
        // Só o candidato cadastra e edita o próprio currículo.
        // A empresa (administrador/recrutador) apenas lê e muda o status.

        // Atalho do menu: abre o currículo do candidato logado (ou o cadastro, se ainda não tem)
        [HttpGet]
        [Acesso(NivelAcesso.Candidato)]
        public IActionResult MeuCurriculo()
        {
            int? id = _curriculoRepository.ObterIdPorUsuario(UsuarioLogadoId);
            return id.HasValue
                ? RedirectToAction(nameof(Editar), new { id = id.Value })
                : RedirectToAction(nameof(Cadastrar));
        }

        [HttpGet]
        [Acesso(NivelAcesso.Candidato)]
        public IActionResult Cadastrar()
        {
            if (_curriculoRepository.ObterIdPorUsuario(UsuarioLogadoId).HasValue)
            {
                return RedirectToAction(nameof(MeuCurriculo));
            }

            // Já começa com o nome e o e-mail da conta
            var usuario = _usuarioRepository.ObterPorId(UsuarioLogadoId);
            var form = new CurriculoFormViewModel
            {
                Nome = usuario?.Nome ?? string.Empty,
                Email = usuario?.Email ?? string.Empty
            };
            form.CompletarExperiencias();
            PreencherSugestoes(form);
            return View("Formulario", form);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Acesso(NivelAcesso.Candidato)]
        public IActionResult Cadastrar(CurriculoFormViewModel form)
        {
            if (_curriculoRepository.ObterIdPorUsuario(UsuarioLogadoId).HasValue)
            {
                return RedirectToAction(nameof(MeuCurriculo));
            }

            form.Id = null;

            if (!ModelState.IsValid)
            {
                form.CompletarExperiencias();
                PreencherSugestoes(form);
                return View("Formulario", form);
            }

            var curriculo = form.ParaCurriculo();
            curriculo.DataCadastro = DateTime.Now;
            curriculo.FotoUrl = string.Empty;
            curriculo.UsuarioId = UsuarioLogadoId;
            curriculo.Status = StatusCurriculo.Novo; // o status é definido pela empresa

            _curriculoRepository.Inserir(curriculo);

            TempData["Mensagem"] = "Seu currículo foi cadastrado.";
            return RedirectToAction(nameof(MeuCurriculo));
        }

        [HttpGet]
        [Acesso(NivelAcesso.Candidato)]
        public IActionResult Editar(int id)
        {
            var curriculo = _curriculoRepository.ObterPorId(id);
            if (curriculo == null)
            {
                return NotFound();
            }
            if (curriculo.UsuarioId != UsuarioLogadoId)
            {
                return AcessoNegado();
            }

            var form = CurriculoFormViewModel.DeCurriculo(curriculo);
            PreencherSugestoes(form);
            return View("Formulario", form);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Acesso(NivelAcesso.Candidato)]
        public IActionResult Editar(int id, CurriculoFormViewModel form)
        {
            var existente = _curriculoRepository.ObterPorId(id);
            if (existente == null)
            {
                return NotFound();
            }
            if (existente.UsuarioId != UsuarioLogadoId)
            {
                return AcessoNegado();
            }

            form.Id = id;

            if (!ModelState.IsValid)
            {
                form.FotoUrl = existente.FotoUrl;
                form.Status = existente.Status;
                form.CompletarExperiencias();
                PreencherSugestoes(form);
                return View("Formulario", form);
            }

            var curriculo = form.ParaCurriculo();
            curriculo.Id = id;
            curriculo.FotoUrl = existente.FotoUrl; // mantém a foto que já existia
            curriculo.Status = existente.Status;   // o candidato não muda o próprio status

            _curriculoRepository.Atualizar(curriculo);

            TempData["Mensagem"] = "Seu currículo foi atualizado.";
            return RedirectToAction(nameof(MeuCurriculo));
        }

        // ============ AÇÕES RÁPIDAS (empresa) ============

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Acesso(NivelAcesso.Empresa)]
        public IActionResult AlterarStatus(int id, string status, string? returnUrl)
        {
            if (_curriculoRepository.AlterarStatus(id, status))
            {
                TempData["Mensagem"] = $"Status alterado para \"{status}\".";
            }

            return VoltarPara(returnUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Acesso(NivelAcesso.Empresa)]
        public IActionResult AlternarFavorito(int id, string? returnUrl)
        {
            if (_curriculoRepository.Existe(id))
            {
                bool favoritou = _favoritoService.AlternarFavorito(Request, Response, id);
                TempData["Mensagem"] = favoritou ? "Adicionado aos favoritos." : "Removido dos favoritos.";
            }

            return VoltarPara(returnUrl);
        }

        // Endereço antigo de detalhes: abre o currículo no painel do dashboard
        [HttpGet]
        [Acesso(NivelAcesso.Empresa)]
        public IActionResult Detalhes(int id)
        {
            return RedirectToAction(nameof(Index), new { selecionado = id });
        }

        // ============ TELAS JÁ EXISTENTES ============

        // Diagnóstico do banco: mostra mensagens de erro internas, então fica só para a equipe
        [HttpGet]
        [Acesso(NivelAcesso.Superadmin)]
        public IActionResult TestDb()
        {
            try
            {
                using var conexao = Conexao.CriarConexao();
                conexao.Open();
                return Content("Conectado ao banco com sucesso!");
            }
            catch (Exception ex)
            {
                return Content("Erro ao conectar:\n" + ex.Message);
            }
        }

        [HttpGet]
        [Publico]
        public IActionResult Login()
        {
            return View(new LoginViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Publico]
        public IActionResult Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var usuario = new AutenticacaoService().Entrar(HttpContext.Session, model.Email, model.Senha);
            if (usuario == null)
            {
                ModelState.AddModelError(string.Empty, "E-mail ou senha inválidos.");
                return View(model);
            }

            TempData["Mensagem"] = $"Bem-vindo(a), {usuario.Nome}!";
            return RedirectToAction(nameof(Index));
        }

        // ============ AUXILIARES ============

        // As ações que usam isto só rodam com login (garantido pelo ControleAcessoFilter)
        private int UsuarioLogadoId => SessaoUsuario.Id(HttpContext.Session)!.Value;

        private IActionResult AcessoNegado()
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return View("AcessoNegado");
        }

        private IActionResult VoltarPara(string? returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToAction(nameof(Index));
        }

        private void PreencherSugestoes(CurriculoFormViewModel form)
        {
            form.SugestoesAreas = _curriculoRepository.ListarAreas();
            form.SugestoesFormacoes = _curriculoRepository.ListarFormacoes();
            form.SugestoesCidades = _curriculoRepository.ListarCidades();
        }
    }
}
