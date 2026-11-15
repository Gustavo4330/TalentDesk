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
        public string Cargo { get; set; } = string.Empty;
        public string Area { get; set; } = string.Empty;

        // Curso (ex.: "Psicologia") e descrição completa (ex.: "Psicologia – Bacharelado")
        public string Formacao { get; set; } = string.Empty;
        public string FormacaoDetalhada { get; set; } = string.Empty;
        public string Instituicao { get; set; } = string.Empty;
        public string ConclusaoFormacao { get; set; } = string.Empty;

        public int Idade { get; set; }
        public int AnosExperiencia { get; set; }
        public string FotoUrl { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Telefone { get; set; } = string.Empty;
        public string Cidade { get; set; } = string.Empty;
        public string Linkedin { get; set; } = string.Empty;
        public string Resumo { get; set; } = string.Empty;
        public string Status { get; set; } = StatusCurriculo.Novo;
        public DateTime DataCadastro { get; set; } = DateTime.Now;

        // Conta do candidato dono do currículo (null nos currículos de exemplo)
        public int? UsuarioId { get; set; }

        public List<ExperienciaProfissional> Experiencias { get; set; } = new();
        public List<string> Habilidades { get; set; } = new();

        // ===== Informações calculadas para exibir na tela =====

        public string Iniciais
        {
            get
            {
                var partes = Nome.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (partes.Length == 0) return "?";
                if (partes.Length == 1) return partes[0][..1].ToUpper();
                return (partes[0][..1] + partes[^1][..1]).ToUpper();
            }
        }

        public string ExperienciaTexto => AnosExperiencia switch
        {
            <= 0 => "Menos de 1 ano",
            1 => "1 ano",
            _ => $"{AnosExperiencia} anos"
        };

        public string CadastradoHa => TempoDesde(DataCadastro);

        public string FormacaoCompleta =>
            string.IsNullOrWhiteSpace(FormacaoDetalhada) ? Formacao : FormacaoDetalhada;

        public string LinkedinUrl
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Linkedin)) return string.Empty;
                return Linkedin.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? Linkedin
                    : "https://" + Linkedin;
            }
        }

        // Ex.: "Cadastrado há 3 dias" (ou "Publicada há 3 dias", com outro prefixo)
        public static string TempoDesde(DateTime data, string prefixo = "Cadastrado")
        {
            var dias = (int)(DateTime.Today - data.Date).TotalDays;

            if (dias <= 0) return $"{prefixo} hoje";
            if (dias == 1) return $"{prefixo} ontem";
            if (dias < 7) return $"{prefixo} há {dias} dias";
            if (dias < 14) return $"{prefixo} há 1 semana";
            if (dias < 30) return $"{prefixo} há {dias / 7} semanas";
            if (dias < 60) return $"{prefixo} há 1 mês";
            if (dias < 365) return $"{prefixo} há {dias / 30} meses";
            return dias < 730 ? $"{prefixo} há 1 ano" : $"{prefixo} há {dias / 365} anos";
        }
    }
}
