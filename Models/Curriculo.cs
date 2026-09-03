namespace TalentDesk.Models
{
    public class ExperienciaProfissional
    {
        public string Cargo { get; set; } = string.Empty;
        public string Empresa { get; set; } = string.Empty;
        public string Periodo { get; set; } = string.Empty;
        public string Descricao { get; set; } = string.Empty;
    }

    public class Curriculo
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Area { get; set; } = string.Empty;
        public string Formacao { get; set; } = string.Empty;
        public string FormacaoDetalhada { get; set; } = string.Empty;
        public int Idade { get; set; }
        public string FotoUrl { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Telefone { get; set; } = string.Empty;
        public string Cidade { get; set; } = string.Empty;
        public string Linkedin { get; set; } = string.Empty;
        public string Resumo { get; set; } = string.Empty;
        public List<ExperienciaProfissional> Experiencias { get; set; } = new();
        public List<string> Habilidades { get; set; } = new();
    }
}