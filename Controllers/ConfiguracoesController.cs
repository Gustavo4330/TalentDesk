using Microsoft.AspNetCore.Mvc;
using TalentDesk.Models;
using TalentDesk.Repositories;
using TalentDesk.Services;

namespace TalentDesk.Controllers
{
    // Aba "Configurações": dados da conta, troca de senha e exclusão da conta.
    // Todas as ações exigem que o usuário esteja logado.
    public class ConfiguracoesController : Controller
    {
        // Fotos enviadas ficam em wwwroot/uploads/usuarios (pasta ignorada pelo git)
        private const string PastaFotos = "uploads/usuarios";
        private const long TamanhoMaximoFoto = 2 * 1024 * 1024; // 2 MB
        private static readonly string[] ExtensoesFoto = { ".jpg", ".jpeg", ".png", ".webp" };

        private readonly UsuarioRepository _usuarioRepository;
        private readonly IWebHostEnvironment _ambiente;

        public ConfiguracoesController(IWebHostEnvironment ambiente)
        {
            _usuarioRepository = new UsuarioRepository();
            _ambiente = ambiente;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var usuario = ObterUsuarioLogado();
            if (usuario == null)
            {
                return IrParaEntrar();
            }

            return View(MontarViewModel(usuario));
        }

        // ============ MEU PERFIL ============

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SalvarPerfil([Bind(Prefix = "Perfil")] PerfilConfiguracaoForm form)
        {
            var usuario = ObterUsuarioLogado();
            if (usuario == null)
            {
                return IrParaEntrar();
            }

            form.Email = form.Email?.Trim() ?? string.Empty;

            if (form.DataNascimento > DateTime.Today)
            {
                ModelState.AddModelError("Perfil.DataNascimento", "A data de nascimento não pode ser no futuro.");
            }

            if (ModelState.IsValid && _usuarioRepository.EmailExisteEmOutraConta(form.Email, usuario.Id))
            {
                ModelState.AddModelError("Perfil.Email", "Este e-mail já está sendo usado por outra conta.");
            }

            if (form.Foto != null && form.Foto.Length > 0)
            {
                string? erroFoto = ValidarFoto(form.Foto);
                if (erroFoto != null)
                {
                    ModelState.AddModelError("Perfil.Foto", erroFoto);
                }
            }

            if (!ModelState.IsValid)
            {
                var viewModel = MontarViewModel(usuario);
                viewModel.Perfil = form;
                viewModel.SecaoAtiva = "perfil";
                return View(nameof(Index), viewModel);
            }

            string? fotoAntiga = usuario.FotoUrl;
            string? novaFoto = fotoAntiga;

            if (form.Foto != null && form.Foto.Length > 0)
            {
                novaFoto = SalvarFoto(form.Foto, usuario.Id);
            }
            else if (form.RemoverFoto)
            {
                novaFoto = null;
            }

            usuario.Nome = form.Nome.Trim();
            usuario.Email = form.Email;
            usuario.Whatsapp = string.IsNullOrWhiteSpace(form.Whatsapp) ? null : form.Whatsapp.Trim();
            usuario.DataNascimento = form.DataNascimento;
            usuario.FotoUrl = novaFoto;

            _usuarioRepository.AtualizarPerfil(usuario);

            // A foto antiga só é apagada depois de gravar a nova no banco
            if (fotoAntiga != novaFoto)
            {
                ApagarFoto(fotoAntiga);
            }

            // Atualiza o nome e a foto mostrados no menu lateral
            SessaoUsuario.AtualizarDados(HttpContext.Session, usuario.Nome, usuario.FotoUrl);

            TempData["Mensagem"] = "Seus dados foram atualizados.";
            return RedirectToAction(nameof(Index));
        }

        // ============ SEGURANÇA ============

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AlterarSenha([Bind(Prefix = "Senha")] AlterarSenhaForm form)
        {
            var usuario = ObterUsuarioLogado();
            if (usuario == null)
            {
                return IrParaEntrar();
            }

            if (ModelState.IsValid && !BCrypt.Net.BCrypt.Verify(form.SenhaAtual, usuario.Senha))
            {
                ModelState.AddModelError("Senha.SenhaAtual", "A senha atual está incorreta.");
            }

            if (ModelState.IsValid && form.NovaSenha == form.SenhaAtual)
            {
                ModelState.AddModelError("Senha.NovaSenha", "A nova senha deve ser diferente da atual.");
            }

            if (!ModelState.IsValid)
            {
                var viewModel = MontarViewModel(usuario);
                viewModel.SecaoAtiva = "seguranca";
                return View(nameof(Index), viewModel);
            }

            _usuarioRepository.AlterarSenha(usuario.Id, form.NovaSenha);

            TempData["Mensagem"] = "Senha alterada com sucesso.";
            return RedirectToAction(nameof(Index));
        }

        // ============ CONTA ============

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ExcluirConta([Bind(Prefix = "Exclusao")] ExcluirContaForm form)
        {
            var usuario = ObterUsuarioLogado();
            if (usuario == null)
            {
                return IrParaEntrar();
            }

            if (ModelState.IsValid && !BCrypt.Net.BCrypt.Verify(form.Senha, usuario.Senha))
            {
                ModelState.AddModelError("Exclusao.Senha", "Senha incorreta.");
            }

            if (!ModelState.IsValid)
            {
                var viewModel = MontarViewModel(usuario);
                viewModel.Exclusao.Confirmo = form.Confirmo;
                viewModel.SecaoAtiva = "conta";
                return View(nameof(Index), viewModel);
            }

            _usuarioRepository.Excluir(usuario.Id);
            ApagarFoto(usuario.FotoUrl);
            SessaoUsuario.Sair(HttpContext.Session);

            TempData["Mensagem"] = "Sua conta foi excluída.";
            return RedirectToAction("Index", "Curriculo");
        }

        // ============ AUXILIARES ============

        // Usuário da sessão, lido de novo do banco. Null se não estiver logado
        // ou se a conta não existir mais.
        private Usuario? ObterUsuarioLogado()
        {
            int? id = SessaoUsuario.Id(HttpContext.Session);
            if (id == null)
            {
                return null;
            }

            var usuario = _usuarioRepository.ObterPorId(id.Value);
            if (usuario == null)
            {
                SessaoUsuario.Sair(HttpContext.Session);
            }

            return usuario;
        }

        // Manda para a tela de entrar e volta para cá depois do login
        private IActionResult IrParaEntrar()
        {
            return RedirectToAction("Entrar", "Usuario", new { returnUrl = Url.Action(nameof(Index)) });
        }

        private ConfiguracoesViewModel MontarViewModel(Usuario usuario)
        {
            return new ConfiguracoesViewModel
            {
                Perfil = new PerfilConfiguracaoForm
                {
                    Nome = usuario.Nome,
                    Email = usuario.Email,
                    Whatsapp = usuario.Whatsapp,
                    DataNascimento = usuario.DataNascimento
                },
                NomePerfil = _usuarioRepository.ObterPerfil(usuario.Id),
                DataCadastro = usuario.DataCadastro,
                FotoUrl = usuario.FotoUrl,
                Iniciais = SessaoUsuario.Iniciais(HttpContext.Session)
            };
        }

        private static string? ValidarFoto(IFormFile foto)
        {
            string extensao = Path.GetExtension(foto.FileName).ToLowerInvariant();

            if (!ExtensoesFoto.Contains(extensao) || !foto.ContentType.StartsWith("image/"))
            {
                return "Envie uma imagem JPG, PNG ou WEBP.";
            }

            if (foto.Length > TamanhoMaximoFoto)
            {
                return "A foto pode ter no máximo 2 MB.";
            }

            return null;
        }

        // Grava com um nome gerado pelo sistema (nunca o nome enviado pelo usuário)
        private string SalvarFoto(IFormFile foto, int usuarioId)
        {
            string pasta = Path.Combine(_ambiente.WebRootPath, PastaFotos);
            Directory.CreateDirectory(pasta);

            string extensao = Path.GetExtension(foto.FileName).ToLowerInvariant();
            string nomeArquivo = $"{usuarioId}-{Guid.NewGuid():N}{extensao}";

            using (var arquivo = System.IO.File.Create(Path.Combine(pasta, nomeArquivo)))
            {
                foto.CopyTo(arquivo);
            }

            return $"/{PastaFotos}/{nomeArquivo}";
        }

        // Só apaga fotos enviadas pelo sistema; links externos são ignorados
        private void ApagarFoto(string? fotoUrl)
        {
            if (string.IsNullOrEmpty(fotoUrl) || !fotoUrl.StartsWith($"/{PastaFotos}/"))
            {
                return;
            }

            string caminho = Path.Combine(_ambiente.WebRootPath, PastaFotos, Path.GetFileName(fotoUrl));
            if (System.IO.File.Exists(caminho))
            {
                System.IO.File.Delete(caminho);
            }
        }
    }
}
