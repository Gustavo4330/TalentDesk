using Microsoft.AspNetCore.Mvc;
using TalentDesk.Filters;
using TalentDesk.Models;
using TalentDesk.Repositories;
using TalentDesk.Services;

namespace TalentDesk.Controllers
{
    // Telas de conta: todas abertas para visitantes (é por aqui que eles entram)
    [Publico]
    public class UsuarioController : Controller
    {
        private readonly UsuarioRepository _usuarioRepository;
        private readonly ConviteRepository _conviteRepository;

        public UsuarioController()
        {
            _usuarioRepository = new UsuarioRepository();
            _conviteRepository = new ConviteRepository();
        }

        // O formulário de criar conta fica na tela estilizada /Curriculo/Login
        [HttpGet]
        public IActionResult Cadastro()
        {
            return RedirectToAction("Login", "Curriculo");
        }

        // ============ ENTRAR / SAIR ============

        [HttpGet]
        public IActionResult Entrar(string? returnUrl)
        {
            // Quem já está logado não precisa ver o login de novo
            if (SessaoUsuario.EstaLogado(HttpContext.Session))
            {
                return Inicio();
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View(new EntrarViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Entrar(EntrarViewModel model, string? returnUrl)
        {
            ViewData["ReturnUrl"] = returnUrl;

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

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return Inicio();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Sair()
        {
            SessaoUsuario.Sair(HttpContext.Session);
            TempData["MensagemSucesso"] = "Você saiu da sua conta.";
            return RedirectToAction(nameof(Entrar));
        }

        // ============ CONVITE DE ADMINISTRADOR ============
        // O link é gerado pela equipe Talent Desk (tela Convites) e enviado ao cliente.

        [HttpGet]
        public IActionResult Convite(string? token)
        {
            var convite = _conviteRepository.ObterValido(token);
            if (convite == null)
            {
                return View("ConviteInvalido");
            }

            ViewData["Token"] = token;
            return View(new ConviteCadastroViewModel { Email = convite.Email });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Convite(string? token, ConviteCadastroViewModel model)
        {
            var convite = _conviteRepository.ObterValido(token);
            if (convite == null)
            {
                return View("ConviteInvalido");
            }

            // O e-mail é sempre o do convite, mesmo que o formulário seja alterado
            model.Email = convite.Email;
            ViewData["Token"] = token;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (_usuarioRepository.EmailExiste(convite.Email))
            {
                ModelState.AddModelError(string.Empty,
                    "Já existe uma conta com este e-mail. Fale com a equipe Talent Desk para receber um novo convite.");
                return View(model);
            }

            try
            {
                _usuarioRepository.CadastrarComConvite(
                    new Usuario { Nome = model.Nome.Trim(), Email = convite.Email, Senha = model.Senha },
                    convite.Id);
            }
            catch (ConviteInvalidoException)
            {
                // Alguém usou o mesmo link ao mesmo tempo
                return View("ConviteInvalido");
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 19)
            {
                ModelState.AddModelError(string.Empty, "Já existe uma conta com este e-mail.");
                return View(model);
            }

            TempData["MensagemSucesso"] = "Conta de administrador criada. Entre com seu e-mail e senha.";
            return RedirectToAction(nameof(Entrar));
        }

        // ============ TELAS JÁ EXISTENTES ============

        [HttpGet]
        public IActionResult Login()
        {
            return View("~/Views/Curriculo/Login.cshtml", new LoginViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Login(LoginViewModel model)
        {
            if (model.DataNascimento?.Date > DateTime.Today)
            {
                ModelState.AddModelError(nameof(model.DataNascimento), "A data de nascimento não pode ser futura.");
            }

            if (!ModelState.IsValid)
            {
                return View("~/Views/Curriculo/Login.cshtml", model);
            }

            TempData["MensagemSucesso"] = $"Bem-vindo(a), {model.Nome}! Seu acesso foi validado.";
            return RedirectToAction("Index", "Curriculo");
        }

        // Cadastro aberto do site: cria sempre uma conta de candidato
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Cadastro(Usuario usuario)
        {
            if (usuario.DataNascimento?.Date > DateTime.Today)
            {
                ModelState.AddModelError(nameof(usuario.DataNascimento), "A data de nascimento não pode ser futura.");
            }

            if (!ModelState.IsValid)
            {
                return TelaDeCadastro(usuario);
            }

            try
            {
                if (_usuarioRepository.EmailExiste(usuario.Email))
                {
                    ModelState.AddModelError("Email", "Este email já está cadastrado.");
                    return TelaDeCadastro(usuario);
                }

                _usuarioRepository.Cadastrar(usuario);

                TempData["MensagemSucesso"] = "Cadastro realizado com sucesso. Faça login para continuar.";
                return RedirectToAction(nameof(Entrar));
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 19)
            {
                // Dois envios ao mesmo tempo (ex.: clique duplo): o banco barra o e-mail repetido
                ModelState.AddModelError("Email", "Este email já está cadastrado.");
                return TelaDeCadastro(usuario);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erro ao cadastrar usuário: " + ex.Message);
                ModelState.AddModelError("", "Ocorreu um erro ao realizar o cadastro. Tente novamente.");
                return TelaDeCadastro(usuario);
            }
        }

        // Volta para a tela estilizada de criar conta mostrando os erros.
        // Os nomes dos campos são os mesmos, então as mensagens aparecem em cada campo.
        private IActionResult TelaDeCadastro(Usuario usuario)
        {
            var model = new LoginViewModel
            {
                Nome = usuario.Nome,
                Email = usuario.Email,
                Whatsapp = usuario.Whatsapp,
                DataNascimento = usuario.DataNascimento
            };

            return View("~/Views/Curriculo/Login.cshtml", model);
        }

        // Tela inicial de cada perfil
        private IActionResult Inicio() =>
            Perfis.EhCandidato(SessaoUsuario.Perfil(HttpContext.Session))
                ? RedirectToAction("MeuCurriculo", "Curriculo")
                : RedirectToAction("Index", "Curriculo");
    }
}
