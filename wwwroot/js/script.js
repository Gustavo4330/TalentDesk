// Toda a regra de negócio fica no C# (controllers e repositórios).
// Este arquivo só cuida de pequenos comportamentos da interface.
document.addEventListener('DOMContentLoaded', () => {
    // Selects de filtro: envia o formulário assim que muda a opção
    document.querySelectorAll('select[data-auto-submit]').forEach((select) => {
        select.addEventListener('change', () => select.form?.submit());
    });

    // Pede confirmação antes de ações perigosas (ex.: excluir currículo)
    document.querySelectorAll('form[data-confirmar]').forEach((form) => {
        form.addEventListener('submit', (event) => {
            if (!window.confirm(form.dataset.confirmar)) {
                event.preventDefault();
            }
        });
    });

    // Menus suspensos (<details>): abre um de cada vez e fecha ao clicar fora
    const menus = Array.from(document.querySelectorAll('details.menu-acoes, details.notificacoes, details.mais-filtros'));

    menus.forEach((menu) => {
        menu.addEventListener('toggle', () => {
            if (!menu.open) return;
            menus.forEach((outro) => {
                if (outro !== menu) outro.open = false;
            });
        });
    });

    document.addEventListener('click', (event) => {
        menus.forEach((menu) => {
            if (menu.open && !menu.contains(event.target)) menu.open = false;
        });
    });

    document.addEventListener('keydown', (event) => {
        if (event.key === 'Escape') menus.forEach((menu) => { menu.open = false; });
    });

    // Foto que não carregou: remove a imagem e deixa aparecer as iniciais
    document.querySelectorAll('.avatar img').forEach((img) => {
        const remover = () => img.remove();
        if (img.complete && img.naturalWidth === 0) remover();
        else img.addEventListener('error', remover);
    });

    // Dica flutuante dos gráficos de relatórios (colunas e bolinhas do mapa)
    const dica = document.querySelector('.dica-flutuante');
    if (dica) {
        const mostrar = (alvo) => {
            const caixa = alvo.getBoundingClientRect();
            dica.textContent = alvo.dataset.dica;
            dica.style.left = `${caixa.left + caixa.width / 2}px`;
            dica.style.top = `${caixa.top}px`;
            dica.hidden = false;
        };
        const esconder = () => { dica.hidden = true; };

        document.querySelectorAll('[data-dica]').forEach((alvo) => {
            alvo.addEventListener('mouseenter', () => mostrar(alvo));
            alvo.addEventListener('focus', () => mostrar(alvo));
            alvo.addEventListener('mouseleave', esconder);
            alvo.addEventListener('blur', esconder);
        });
        window.addEventListener('scroll', esconder, { passive: true });
    }

    // Celular: menu lateral abre e fecha pelo botão hambúrguer
    const menu = document.querySelector('[data-menu]');
    const botaoMenu = document.querySelector('[data-menu-botao]');
    if (menu && botaoMenu) {
        const rotulo = botaoMenu.querySelector('.sr-only');
        const definirAberto = (aberto) => {
            menu.classList.toggle('aberto', aberto);
            botaoMenu.setAttribute('aria-expanded', String(aberto));
            if (rotulo) rotulo.textContent = aberto ? 'Fechar menu' : 'Abrir menu';
        };

        botaoMenu.addEventListener('click', () => definirAberto(!menu.classList.contains('aberto')));

        // Fecha com Esc (devolvendo o foco ao botão) ou tocando fora do menu
        document.addEventListener('keydown', (evento) => {
            if (evento.key === 'Escape' && menu.classList.contains('aberto')) {
                definirAberto(false);
                botaoMenu.focus();
            }
        });
        document.addEventListener('click', (evento) => {
            if (menu.classList.contains('aberto') && !menu.contains(evento.target)) definirAberto(false);
        });

        // Ao voltar para a tela grande, o menu não fica preso aberto
        window.matchMedia('(min-width: 761px)').addEventListener('change', (consulta) => {
            if (consulta.matches) definirAberto(false);
        });
    }

    // Botão "Copiar" (ex.: link de convite)
    document.querySelectorAll('[data-copiar]').forEach((botao) => {
        botao.addEventListener('click', async () => {
            const campo = document.querySelector(botao.dataset.copiar);
            if (!campo) return;
            try {
                await navigator.clipboard.writeText(campo.value);
            } catch {
                campo.select();
                document.execCommand('copy');
            }
            const texto = botao.textContent;
            botao.textContent = 'Copiado!';
            window.setTimeout(() => { botao.textContent = texto; }, 2000);
        });
    });

    // Dashboard: alterna entre mapa e lista de cidades
    document.querySelectorAll('[data-alternar]').forEach((card) => {
        const botoes = card.querySelectorAll('[data-alvo]');
        botoes.forEach((botao) => {
            botao.addEventListener('click', () => {
                botoes.forEach((b) => b.setAttribute('aria-pressed', String(b === botao)));
                card.querySelectorAll('[data-painel]').forEach((painel) => {
                    painel.hidden = painel.dataset.painel !== botao.dataset.alvo;
                });
            });
        });
    });

    // Dashboard: "Exportar relatório" abre a impressão (dá para salvar em PDF)
    document.querySelectorAll('[data-imprimir]').forEach((botao) => {
        botao.addEventListener('click', () => window.print());
    });

    // Esconde a mensagem de sucesso depois de alguns segundos
    const toast = document.querySelector('.toast');
    if (toast) {
        window.setTimeout(() => toast.classList.add('saindo'), 4000);
    }
});
