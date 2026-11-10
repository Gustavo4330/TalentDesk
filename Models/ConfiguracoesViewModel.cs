using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace TalentDesk.Models
{
    // Tela de Configurações: cada seção tem o seu formulário
    public class ConfiguracoesViewModel
    {
        public PerfilConfiguracaoForm Perfil { get; set; } = new();
        public AlterarSenhaForm Senha { get; set; } = new();
        public ExcluirContaForm Exclusao { get; set; } = new();

        // Dados só para mostrar (não vêm dos formulários)
        public string NomePerfil { get; set; } = string.Empty;
        public DateTime? DataCadastro { get; set; }
        public string? FotoUrl { get; set; }
        public string Iniciais { get; set; } = "?";

        // Qual seção mostrar aberta (ex.: depois de um erro na troca de senha)
        public string SecaoAtiva { get; set; } = "perfil";
    }

    public class PerfilConfiguracaoForm
    {
        [Required(ErrorMessage = "Informe seu nome.")]
        [StringLength(150, ErrorMessage = "O nome pode ter até 150 caracteres.")]
        [Display(Name = "Nome completo")]
        public string Nome { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe seu e-mail.")]
        [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
        [StringLength(150, ErrorMessage = "O e-mail pode ter até 150 caracteres.")]
        [Display(Name = "E-mail")]
        public string Email { get; set; } = string.Empty;

        [StringLength(20, ErrorMessage = "O WhatsApp pode ter até 20 caracteres.")]
        [Display(Name = "WhatsApp")]
        public string? Whatsapp { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Data de nascimento")]
        public DateTime? DataNascimento { get; set; }

        // Nova foto enviada do computador (opcional)
        [Display(Name = "Foto")]
        public IFormFile? Foto { get; set; }

        public bool RemoverFoto { get; set; }
    }

    public class AlterarSenhaForm
    {
        [Required(ErrorMessage = "Informe a senha atual.")]
        [DataType(DataType.Password)]
        [Display(Name = "Senha atual")]
        public string SenhaAtual { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe a nova senha.")]
        [MinLength(6, ErrorMessage = "A nova senha deve ter pelo menos 6 caracteres.")]
        [DataType(DataType.Password)]
        [Display(Name = "Nova senha")]
        public string NovaSenha { get; set; } = string.Empty;

        [Required(ErrorMessage = "Confirme a nova senha.")]
        [Compare(nameof(NovaSenha), ErrorMessage = "A confirmação não confere com a nova senha.")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirmar nova senha")]
        public string ConfirmarSenha { get; set; } = string.Empty;
    }

    public class ExcluirContaForm
    {
        [Required(ErrorMessage = "Informe sua senha para confirmar.")]
        [DataType(DataType.Password)]
        [Display(Name = "Senha")]
        public string Senha { get; set; } = string.Empty;

        [Range(typeof(bool), "true", "true", ErrorMessage = "Marque a confirmação para excluir a conta.")]
        public bool Confirmo { get; set; }
    }
}
