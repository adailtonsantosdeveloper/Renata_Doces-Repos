'use strict';

/*
 * Renata Doces - front-end
 *
 * Regras de segurança deste arquivo:
 *  - Nada de innerHTML com dados vindos da API: tudo é montado com textContent/createElement
 *    (impede XSS, mesmo que alguém cadastre "<script>" como nome de cliente).
 *  - Sem handlers inline (onclick=...): compatível com a Content-Security-Policy do servidor.
 *  - A API usa cookie de sessão HttpOnly na mesma origem; o JavaScript nunca vê a senha guardada.
 */
(() => {
    const API = '/api';
    const MAX_QUANTIDADE = 10000;

    // ------------------------------------------------------------------
    // Utilidades
    // ------------------------------------------------------------------
    const $ = (id) => document.getElementById(id);

    const moeda = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' });
    const dataHora = new Intl.DateTimeFormat('pt-BR', {
        day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit'
    });

    /** Cria um elemento. Textos entram como texto puro (nunca como HTML). */
    function el(tag, props = {}, ...children) {
        const node = document.createElement(tag);
        for (const [chave, valor] of Object.entries(props)) {
            if (valor === null || valor === undefined || valor === false) continue;
            if (chave === 'class') node.className = valor;
            else if (chave === 'text') node.textContent = valor;
            else if (chave.startsWith('on') && typeof valor === 'function') node.addEventListener(chave.slice(2), valor);
            else node.setAttribute(chave, valor === true ? '' : String(valor));
        }
        for (const filho of children.flat()) {
            if (filho === null || filho === undefined || filho === false) continue;
            node.append(filho instanceof Node ? filho : document.createTextNode(String(filho)));
        }
        return node;
    }

    /** Aceita "12,50", "12.50" e "1.234,50". Devolve NaN se não for número. */
    function lerDecimal(texto) {
        let s = String(texto ?? '').trim().replace(/\s|R\$/g, '');
        if (s.includes(',')) s = s.replace(/\./g, '').replace(',', '.');
        if (!/^\d+(\.\d{1,2})?$/.test(s)) return NaN;   // 'ex.: 1.234' é ambíguo: rejeita
        return Number(s);
    }

    /** Minúsculas e sem acento, para a busca. */
    function normalizar(texto) {
        return String(texto ?? '').normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase();
    }

    function formatarData(valor) {
        const data = new Date(valor);
        return Number.isNaN(data.getTime()) ? '' : dataHora.format(data);
    }

    function vazioParaNulo(texto) {
        const t = String(texto ?? '').trim();
        return t === '' ? null : t;
    }

    // ------------------------------------------------------------------
    // Comunicação com a API
    // ------------------------------------------------------------------
    class ApiError extends Error {
        constructor(status, mensagem) {
            super(mensagem);
            this.status = status;
        }
    }

    function mensagemDaResposta(dados, status) {
        if (dados && typeof dados.mensagem === 'string') return dados.mensagem;
        if (dados && dados.errors && typeof dados.errors === 'object') {
            const primeira = Object.values(dados.errors).flat()[0];
            if (typeof primeira === 'string') return primeira;
        }
        if (status === 401) return 'Sessão expirada. Entre novamente.';
        if (status === 403) return 'Você não tem permissão para isso.';
        if (status === 413) return 'Os dados enviados são grandes demais.';
        if (status === 415) return 'Formato de dados não aceito.';
        if (status === 429) return 'Muitas tentativas. Aguarde um minuto e tente de novo.';
        if (status >= 500) return 'Erro no servidor. Tente novamente em instantes.';
        return 'Não foi possível concluir a operação.';
    }

    async function api(caminho, { method = 'GET', body } = {}) {
        const opcoes = { method, credentials: 'same-origin', headers: { Accept: 'application/json' } };
        if (body !== undefined) {
            opcoes.headers['Content-Type'] = 'application/json';
            opcoes.body = JSON.stringify(body);
        }

        let resposta;
        try {
            resposta = await fetch(API + caminho, opcoes);
        } catch {
            throw new ApiError(0, 'Sem conexão com o servidor. Verifique sua internet.');
        }

        if (resposta.status === 204) return null;

        let dados = null;
        const texto = await resposta.text();
        if (texto) {
            try { dados = JSON.parse(texto); } catch { /* corpo não era JSON */ }
        }

        if (!resposta.ok) {
            if (resposta.status === 401 && caminho !== '/auth/login') {
                mostrarLogin('Sessão expirada. Entre novamente.');
            }
            throw new ApiError(resposta.status, mensagemDaResposta(dados, resposta.status));
        }
        return dados;
    }

    function textoDoErro(erro) {
        return erro instanceof ApiError ? erro.message : 'Algo deu errado. Tente novamente.';
    }

    // ------------------------------------------------------------------
    // Aviso rápido (toast)
    // ------------------------------------------------------------------
    let temporizadorToast = null;

    function avisar(mensagem, erro = false) {
        const toast = $('toast');
        toast.textContent = mensagem;
        toast.classList.toggle('erro', erro);
        toast.hidden = false;
        clearTimeout(temporizadorToast);
        temporizadorToast = setTimeout(() => { toast.hidden = true; }, erro ? 5000 : 3000);
    }

    /** Mostra o erro, exceto 401 (nesse caso a tela de login já explica que a sessão expirou). */
    function avisarErro(erro) {
        if (erro instanceof ApiError && erro.status === 401) return;
        avisar(textoDoErro(erro), true);
    }

    // ------------------------------------------------------------------
    // Estado
    // ------------------------------------------------------------------
    // Cada saída/expiração de sessão incrementa o contador; respostas que chegam depois disso são descartadas.
    let sessao = 0;

    const estado = {
        clientes: [],
        produtos: [],
        pedidos: [],
        itensPedido: [{ produtoId: '', quantidade: 1 }],
        editandoClienteId: null,
        editandoProdutoId: null
    };

    // ------------------------------------------------------------------
    // Login / sessão
    // ------------------------------------------------------------------
    function mostrarLogin(mensagem) {
        sessao++;
        estado.clientes = [];
        estado.produtos = [];
        estado.pedidos = [];
        estado.itensPedido = [{ produtoId: '', quantidade: 1 }];
        estado.editandoClienteId = null;
        estado.editandoProdutoId = null;
        $('buscaClientes').value = '';
        $('buscaProdutos').value = '';
        $('pedidoCliente').value = '';
        $('toast').hidden = true;
        $('viewCarregando').hidden = true;

        // Não deixa dados de clientes "esquecidos" na tela depois de sair.
        $('listaClientes').replaceChildren();
        $('listaProdutos').replaceChildren();
        $('listaPedidos').replaceChildren();
        fecharFolhas();

        $('viewApp').hidden = true;
        $('viewCadastro').hidden = true;
        $('viewLogin').hidden = false;
        limparCadastro();
        $('loginInfo').hidden = true;

        const erro = $('loginErro');
        erro.textContent = mensagem || '';
        erro.hidden = !mensagem;
        $('loginSenha').value = '';
    }

    async function entrarNoApp() {
        $('viewCarregando').hidden = true;
        $('viewLogin').hidden = true;
        $('viewApp').hidden = false;
        mostrarAba('clientes');
        await carregarTudo();
    }

    async function aoEnviarLogin(evento) {
        evento.preventDefault();
        const usuario = $('loginUsuario').value.trim();
        const senha = $('loginSenha').value;
        const erro = $('loginErro');
        const botao = evento.submitter || evento.target.querySelector('button[type="submit"]');

        if (!usuario || !senha) {
            erro.textContent = 'Informe usuário e senha.';
            erro.hidden = false;
            return;
        }

        erro.hidden = true;
        botao.disabled = true;
        try {
            await api('/auth/login', { method: 'POST', body: { usuario, senha } });
            $('loginSenha').value = '';
            await entrarNoApp();
        } catch (e) {
            erro.textContent = textoDoErro(e);
            erro.hidden = false;
        } finally {
            botao.disabled = false;
        }
    }

    async function sair() {
        let falhou = false;
        try {
            await api('/auth/logout', { method: 'POST' });
        } catch (e) {
            falhou = !(e instanceof ApiError && e.status === 401);
        }
        mostrarLogin();
        if (falhou) avisar('Não foi possível encerrar a sessão no servidor. Tente sair de novo quando tiver conexão.', true);
    }

    // ------------------------------------------------------------------
    // Cadastro de usuário
    // ------------------------------------------------------------------
    const REGRA_USUARIO = /^[A-Za-z0-9._@-]{3,60}$/;
    const SENHA_MINIMA = 10;
    const SENHA_MAXIMA = 128;

    function limparCadastro() {
        for (const id of ['cadUsuario', 'cadSenha', 'cadConfirmar', 'cadConvite']) $(id).value = '';
        $('cadastroErro').hidden = true;
    }

    async function abrirCadastro() {
        limparCadastro();
        $('loginErro').hidden = true;
        $('loginInfo').hidden = true;
        $('viewLogin').hidden = true;
        $('viewCadastro').hidden = false;
        $('btnCadastrar').disabled = false;
        $('campoConvite').hidden = true;
        $('cadastroAviso').textContent = 'Crie sua conta para acessar a doceria.';
        $('cadUsuario').focus({ preventScroll: true });

        // O servidor diz se pede código de convite ou se o cadastro está fechado.
        const minha = sessao;
        try {
            const status = await api('/auth/cadastro');
            if (minha !== sessao || $('viewCadastro').hidden) return;

            $('campoConvite').hidden = !status.exigeCodigo;
            if (!status.aberto) {
                $('cadastroAviso').textContent = 'O cadastro está fechado. Peça o código de convite a quem administra o sistema.';
                $('btnCadastrar').disabled = true;
            } else if (status.exigeCodigo) {
                $('cadastroAviso').textContent = 'Informe o código de convite que você recebeu.';
            } else if (status.primeiroAcesso) {
                $('cadastroAviso').textContent = 'Primeiro acesso: crie a conta principal do sistema.';
            }
        } catch (e) {
            // Sem resposta: o formulário continua utilizável, e o servidor confere tudo de novo ao enviar.
            avisarErro(e);
        }
    }

    async function aoEnviarCadastro(evento) {
        evento.preventDefault();
        const usuario = $('cadUsuario').value.trim();
        const senha = $('cadSenha').value;
        const confirmarSenha = $('cadConfirmar').value;
        const codigoConvite = $('cadConvite').value.trim();
        const erro = $('cadastroErro');
        const botao = evento.submitter || evento.target.querySelector('button[type="submit"]');

        let problema = null;
        if (!REGRA_USUARIO.test(usuario)) problema = 'O usuário deve ter de 3 a 60 caracteres: letras sem acento, números, ponto, hífen, sublinhado ou @.';
        else if (senha.length < SENHA_MINIMA) problema = `A senha deve ter no mínimo ${SENHA_MINIMA} caracteres.`;
        else if (senha.length > SENHA_MAXIMA) problema = `A senha deve ter no máximo ${SENHA_MAXIMA} caracteres.`;
        else if (senha.toLowerCase() === usuario.toLowerCase()) problema = 'A senha não pode ser igual ao nome de usuário.';
        else if (senha !== confirmarSenha) problema = 'A confirmação da senha não confere.';
        else if (!$('campoConvite').hidden && !codigoConvite) problema = 'Informe o código de convite.';

        if (problema) {
            erro.textContent = problema;
            erro.hidden = false;
            return;
        }

        erro.hidden = true;
        botao.disabled = true;
        try {
            await api('/auth/registrar', {
                method: 'POST',
                body: { usuario, senha, confirmarSenha, codigoConvite: codigoConvite || null }
            });
        } catch (e) {
            erro.textContent = textoDoErro(e);
            erro.hidden = false;
            return;
        } finally {
            botao.disabled = false;
        }

        // Conta criada: volta ao login (sem entrar sozinho), com o usuário já preenchido.
        mostrarLogin();
        $('loginUsuario').value = usuario;
        const info = $('loginInfo');
        info.textContent = 'Conta criada! Entre com seu usuário e senha.';
        info.hidden = false;
        $('loginSenha').focus({ preventScroll: true });
    }

    // ------------------------------------------------------------------
    // Abas
    // ------------------------------------------------------------------
    function mostrarAba(nome) {
        for (const secao of document.querySelectorAll('.aba')) {
            secao.hidden = secao.id !== `aba-${nome}`;
        }
        for (const botao of document.querySelectorAll('.tab')) {
            const ativa = botao.dataset.aba === nome;
            botao.classList.toggle('active', ativa);
            if (ativa) botao.setAttribute('aria-current', 'page');
            else botao.removeAttribute('aria-current');
        }
        if (nome === 'pedidos') {
            montarFormularioPedido();
            carregarPedidos().catch((e) => avisarErro(e));
        }
        window.scrollTo(0, 0);
    }

    async function carregarTudo() {
        try {
            await Promise.all([carregarClientes(), carregarProdutos(), carregarPedidos()]);
        } catch (e) {
            avisarErro(e);
        }
    }

    // ------------------------------------------------------------------
    // Folhas (formulários que sobem da parte de baixo)
    // ------------------------------------------------------------------
    let focoAnterior = null;

    function abrirFolha(folha, primeiroCampo) {
        focoAnterior = document.activeElement;
        folha.hidden = false;
        document.documentElement.classList.add('no-scroll');
        document.body.classList.add('no-scroll');
        // Foco síncrono, dentro do toque do usuário: é o único jeito de o iOS abrir o teclado.
        if (primeiroCampo) primeiroCampo.focus({ preventScroll: true });
    }

    function fecharFolhas() {
        for (const folha of document.querySelectorAll('.sheet')) folha.hidden = true;
        document.documentElement.classList.remove('no-scroll');
        document.body.classList.remove('no-scroll');
        if (focoAnterior && document.contains(focoAnterior)) focoAnterior.focus({ preventScroll: true });
        focoAnterior = null;
    }

    /** O teclado do iPhone não redimensiona a página: a folha acompanha a área realmente visível. */
    function acompanharTeclado() {
        const vv = window.visualViewport;
        if (!vv) return;
        const raiz = document.documentElement.style;
        const ajustar = () => {
            raiz.setProperty('--vvh', `${vv.height}px`);
            raiz.setProperty('--vvt', `${vv.offsetTop}px`);
        };
        vv.addEventListener('resize', ajustar);
        vv.addEventListener('scroll', ajustar);
        ajustar();
    }

    // ------------------------------------------------------------------
    // CLIENTES
    // ------------------------------------------------------------------
    async function carregarClientes() {
        const minha = sessao;
        const dados = await api('/clientes');
        if (minha !== sessao) return;
        estado.clientes = dados;
        renderizarClientes();
        preencherSelectClientes();
    }

    function renderizarClientes() {
        const termo = normalizar($('buscaClientes').value.trim());
        const lista = $('listaClientes');
        const filtrados = estado.clientes.filter((c) =>
            !termo || normalizar(`${c.nome} ${c.telefone ?? ''} ${c.email ?? ''}`).includes(termo));

        if (filtrados.length === 0) {
            lista.replaceChildren(el('li', {
                class: 'empty',
                text: estado.clientes.length === 0 ? 'Nenhum cliente cadastrado ainda.' : 'Nenhum cliente encontrado.'
            }));
            return;
        }

        lista.replaceChildren(...filtrados.map((c) => {
            const telefoneLimpo = (c.telefone ?? '').replace(/[^\d+]/g, '');
            return el('li', { class: 'item' },
                el('div', { class: 'item-title', text: c.nome }),
                c.telefone && el('div', { class: 'item-line' },
                    telefoneLimpo
                        ? el('a', { href: `tel:${telefoneLimpo}`, text: `📞 ${c.telefone}` })
                        : `📞 ${c.telefone}`),
                c.email && el('div', { class: 'item-line', text: `✉️ ${c.email}` }),
                c.endereco && el('div', { class: 'item-line', text: `📍 ${c.endereco}` }),
                el('div', { class: 'item-actions' },
                    el('button', { type: 'button', class: 'btn btn-secondary btn-small', text: 'Editar', 'aria-label': `Editar ${c.nome}`, onclick: () => abrirFormularioCliente(c) }),
                    el('button', { type: 'button', class: 'btn btn-danger btn-small', text: 'Excluir', 'aria-label': `Excluir ${c.nome}`, onclick: () => excluirCliente(c) })));
        }));
    }

    function abrirFormularioCliente(cliente) {
        estado.editandoClienteId = cliente ? cliente.id : null;
        $('tituloFormCliente').textContent = cliente ? 'Editar cliente' : 'Novo cliente';
        $('clienteNome').value = cliente?.nome ?? '';
        $('clienteTelefone').value = cliente?.telefone ?? '';
        $('clienteEmail').value = cliente?.email ?? '';
        $('clienteEndereco').value = cliente?.endereco ?? '';
        $('erroCliente').hidden = true;
        abrirFolha($('sheetCliente'), $('clienteNome'));
    }

    async function salvarCliente(evento) {
        evento.preventDefault();
        const erro = $('erroCliente');
        const botao = evento.submitter || evento.target.querySelector('button[type="submit"]');

        const corpo = {
            nome: $('clienteNome').value.trim(),
            telefone: vazioParaNulo($('clienteTelefone').value),
            email: vazioParaNulo($('clienteEmail').value),
            endereco: vazioParaNulo($('clienteEndereco').value)
        };

        let problema = null;
        if (corpo.nome.length < 2) problema = 'Informe o nome do cliente.';
        else if (corpo.telefone && !/^[0-9+()\-\s]*$/.test(corpo.telefone)) problema = 'O telefone deve conter apenas números, espaços, + ( ) e -.';
        else if (corpo.email && !$('clienteEmail').checkValidity()) problema = 'E-mail inválido.';

        if (problema) {
            erro.textContent = problema;
            erro.hidden = false;
            return;
        }

        erro.hidden = true;
        botao.disabled = true;
        const editando = estado.editandoClienteId !== null;
        try {
            await api(editando ? `/clientes/${estado.editandoClienteId}` : '/clientes', {
                method: editando ? 'PUT' : 'POST',
                body: corpo
            });
        } catch (e) {
            erro.textContent = textoDoErro(e);
            erro.hidden = false;
            return;
        } finally {
            botao.disabled = false;
        }

        fecharFolhas();
        avisar(editando ? 'Cliente atualizado.' : 'Cliente cadastrado.');
        carregarClientes().catch(avisarErro);
    }

    async function excluirCliente(cliente) {
        if (!confirm(`Excluir o cliente "${cliente.nome}"?`)) return;
        try {
            await api(`/clientes/${cliente.id}`, { method: 'DELETE' });
            avisar('Cliente excluído.');
            await carregarClientes();
        } catch (e) {
            avisarErro(e);
        }
    }

    // ------------------------------------------------------------------
    // PRODUTOS
    // ------------------------------------------------------------------
    async function carregarProdutos() {
        const minha = sessao;
        const dados = await api('/produtos');
        if (minha !== sessao) return;
        estado.produtos = dados;
        renderizarProdutos();
        if (!$('aba-pedidos').hidden) montarFormularioPedido();
    }

    function renderizarProdutos() {
        const termo = normalizar($('buscaProdutos').value.trim());
        const lista = $('listaProdutos');
        const filtrados = estado.produtos.filter((p) =>
            !termo || normalizar(`${p.nome} ${p.codigo} ${p.sabor ?? ''} ${p.recheio ?? ''}`).includes(termo));

        if (filtrados.length === 0) {
            lista.replaceChildren(el('li', {
                class: 'empty',
                text: estado.produtos.length === 0 ? 'Nenhum produto cadastrado ainda.' : 'Nenhum produto encontrado.'
            }));
            return;
        }

        lista.replaceChildren(...filtrados.map((p) => {
            const detalhes = [p.sabor, p.recheio && `recheio de ${p.recheio}`, p.peso].filter(Boolean).join(' · ');
            return el('li', { class: 'item' },
                el('div', { class: 'item-title', text: p.nome }),
                el('div', { class: 'item-line', text: `Código: ${p.codigo}` }),
                detalhes && el('div', { class: 'item-line', text: detalhes }),
                el('div', { class: 'item-price', text: moeda.format(p.precoVenda) }),
                el('div', { class: 'item-actions' },
                    el('button', { type: 'button', class: 'btn btn-secondary btn-small', text: 'Editar', 'aria-label': `Editar ${p.nome}`, onclick: () => abrirFormularioProduto(p) }),
                    el('button', { type: 'button', class: 'btn btn-danger btn-small', text: 'Excluir', 'aria-label': `Excluir ${p.nome}`, onclick: () => excluirProduto(p) })));
        }));
    }

    function abrirFormularioProduto(produto) {
        estado.editandoProdutoId = produto ? produto.id : null;
        $('tituloFormProduto').textContent = produto ? 'Editar produto' : 'Novo produto';
        $('produtoCodigo').value = produto?.codigo ?? '';
        $('produtoNome').value = produto?.nome ?? '';
        $('produtoSabor').value = produto?.sabor ?? '';
        $('produtoRecheio').value = produto?.recheio ?? '';
        $('produtoPeso').value = produto?.peso ?? '';
        $('produtoPreco').value = produto ? Number(produto.precoVenda).toFixed(2).replace('.', ',') : '';
        $('produtoIngredientes').value = produto?.ingredientes ?? '';
        $('erroProduto').hidden = true;
        abrirFolha($('sheetProduto'), $('produtoCodigo'));
    }

    async function salvarProduto(evento) {
        evento.preventDefault();
        const erro = $('erroProduto');
        const botao = evento.submitter || evento.target.querySelector('button[type="submit"]');

        const preco = lerDecimal($('produtoPreco').value);
        const corpo = {
            codigo: $('produtoCodigo').value.trim(),
            nome: $('produtoNome').value.trim(),
            sabor: vazioParaNulo($('produtoSabor').value),
            recheio: vazioParaNulo($('produtoRecheio').value),
            peso: vazioParaNulo($('produtoPeso').value),
            precoVenda: Math.round(preco * 100) / 100,
            ingredientes: vazioParaNulo($('produtoIngredientes').value)
        };

        let problema = null;
        if (!corpo.codigo) problema = 'Informe o código do produto.';
        else if (corpo.nome.length < 2) problema = 'Informe o nome do doce.';
        else if (!Number.isFinite(preco) || preco < 0.01 || preco > 999999.99) problema = 'Informe um preço válido (ex.: 4,50).';

        if (problema) {
            erro.textContent = problema;
            erro.hidden = false;
            return;
        }

        erro.hidden = true;
        botao.disabled = true;
        const editando = estado.editandoProdutoId !== null;
        try {
            await api(editando ? `/produtos/${estado.editandoProdutoId}` : '/produtos', {
                method: editando ? 'PUT' : 'POST',
                body: corpo
            });
        } catch (e) {
            erro.textContent = textoDoErro(e);
            erro.hidden = false;
            return;
        } finally {
            botao.disabled = false;
        }

        fecharFolhas();
        avisar(editando ? 'Produto atualizado.' : 'Produto cadastrado.');
        carregarProdutos().catch(avisarErro);
    }

    async function excluirProduto(produto) {
        if (!confirm(`Excluir o produto "${produto.nome}"?`)) return;
        try {
            await api(`/produtos/${produto.id}`, { method: 'DELETE' });
            avisar('Produto excluído.');
            await carregarProdutos();
        } catch (e) {
            avisarErro(e);
        }
    }

    // ------------------------------------------------------------------
    // PEDIDOS
    // ------------------------------------------------------------------
    function preencherSelectClientes() {
        const select = $('pedidoCliente');
        const atual = select.value;
        select.replaceChildren(
            el('option', { value: '', text: 'Selecione o cliente…' }),
            ...estado.clientes.map((c) => el('option', { value: c.id, text: c.nome })));
        if (atual && estado.clientes.some((c) => String(c.id) === atual)) select.value = atual;
    }

    function montarFormularioPedido() {
        preencherSelectClientes();
        renderizarItensPedido();
    }

    function renderizarItensPedido() {
        for (const item of estado.itensPedido) {
            if (item.produtoId !== '' && !estado.produtos.some((p) => String(p.id) === String(item.produtoId))) {
                item.produtoId = '';
            }
        }

        const opcoes = () => [
            el('option', { value: '', text: 'Produto…' }),
            ...estado.produtos.map((p) => el('option', { value: p.id, text: `${p.nome} — ${moeda.format(p.precoVenda)}` }))
        ];

        $('itensPedido').replaceChildren(...estado.itensPedido.map((item, indice) => {
            const select = el('select', { 'aria-label': `Produto do item ${indice + 1}` }, ...opcoes());
            select.value = item.produtoId;
            select.addEventListener('change', () => {
                item.produtoId = select.value;
                atualizarTotalPedido();
            });

            const quantidade = el('input', {
                type: 'number', inputmode: 'numeric', min: 1, max: MAX_QUANTIDADE, step: 1,
                value: item.quantidade, 'aria-label': `Quantidade do item ${indice + 1}`
            });
            quantidade.addEventListener('input', () => {
                item.quantidade = quantidade.value;
                atualizarTotalPedido();
            });

            const remover = el('button', {
                type: 'button', class: 'btn btn-danger', text: '✕', 'aria-label': `Remover item ${indice + 1}`,
                onclick: () => {
                    estado.itensPedido.splice(indice, 1);
                    if (estado.itensPedido.length === 0) estado.itensPedido.push({ produtoId: '', quantidade: 1 });
                    renderizarItensPedido();
                }
            });

            return el('div', { class: 'item-row' }, select, quantidade, remover);
        }));

        atualizarTotalPedido();
    }

    function atualizarTotalPedido() {
        let total = 0;
        for (const item of estado.itensPedido) {
            const produto = estado.produtos.find((p) => String(p.id) === String(item.produtoId));
            const quantidade = Number(item.quantidade);
            if (produto && Number.isInteger(quantidade) && quantidade > 0) total += produto.precoVenda * quantidade;
        }
        $('totalPedido').textContent = moeda.format(total);
    }

    async function carregarPedidos() {
        const minha = sessao;
        const dados = await api('/pedidos?limite=100');
        if (minha !== sessao) return;
        estado.pedidos = dados;
        renderizarPedidos();
    }

    function renderizarPedidos() {
        const lista = $('listaPedidos');
        if (estado.pedidos.length === 0) {
            lista.replaceChildren(el('li', { class: 'empty', text: 'Nenhum pedido registrado ainda.' }));
            return;
        }

        lista.replaceChildren(...estado.pedidos.map((p) => el('li', { class: 'item' },
            el('div', { class: 'item-title', text: `Pedido #${p.id} · ${p.clienteNome}` }),
            el('div', { class: 'item-line', text: formatarData(p.dataPedido) }),
            el('ul', { class: 'pedido-items' },
                p.itens.map((i) => el('li', { text: `${i.quantidade} × ${i.produtoNome} (${moeda.format(i.precoUnitario)})` }))),
            el('div', { class: 'pedido-total', text: `Total: ${moeda.format(p.valorTotal)}` }),
            el('div', { class: 'item-actions' },
                el('button', { type: 'button', class: 'btn btn-danger btn-small', text: 'Cancelar pedido', 'aria-label': `Cancelar pedido ${p.id}`, onclick: () => excluirPedido(p) })))));
    }

    async function registrarPedido(evento) {
        evento.preventDefault();
        const botao = evento.submitter || evento.target.querySelector('button[type="submit"]');

        const clienteId = Number.parseInt($('pedidoCliente').value, 10);
        if (!Number.isInteger(clienteId)) {
            avisar('Selecione o cliente.', true);
            return;
        }

        const itens = [];
        for (const item of estado.itensPedido) {
            if (item.produtoId === '') continue;
            const quantidade = Number(item.quantidade);
            if (!Number.isInteger(quantidade) || quantidade < 1 || quantidade > MAX_QUANTIDADE) {
                avisar(`A quantidade deve ser um número inteiro de 1 a ${MAX_QUANTIDADE}.`, true);
                return;
            }
            itens.push({ produtoId: Number.parseInt(item.produtoId, 10), quantidade });
        }

        if (itens.length === 0) {
            avisar('Adicione pelo menos um produto ao pedido.', true);
            return;
        }

        botao.disabled = true;
        try {
            await api('/pedidos', { method: 'POST', body: { clienteId, itens } });
        } catch (e) {
            avisarErro(e);
            return;
        } finally {
            botao.disabled = false;
        }

        estado.itensPedido = [{ produtoId: '', quantidade: 1 }];
        $('pedidoCliente').value = '';
        renderizarItensPedido();
        avisar('Pedido registrado.');
        carregarPedidos().catch(avisarErro);
    }

    async function excluirPedido(pedido) {
        if (!confirm(`Cancelar o pedido #${pedido.id} de ${pedido.clienteNome}? Isso não pode ser desfeito.`)) return;
        try {
            await api(`/pedidos/${pedido.id}`, { method: 'DELETE' });
            avisar('Pedido cancelado.');
            await carregarPedidos();
        } catch (e) {
            avisarErro(e);
        }
    }

    // ------------------------------------------------------------------
    // Inicialização
    // ------------------------------------------------------------------
    function ligarEventos() {
        acompanharTeclado();
        $('formLogin').addEventListener('submit', aoEnviarLogin);
        $('btnIrCadastro').addEventListener('click', abrirCadastro);
        $('btnVoltarLogin').addEventListener('click', () => mostrarLogin());
        $('formCadastro').addEventListener('submit', aoEnviarCadastro);
        $('btnSair').addEventListener('click', sair);

        for (const botao of document.querySelectorAll('.tab')) {
            botao.addEventListener('click', () => mostrarAba(botao.dataset.aba));
        }

        $('btnNovoCliente').addEventListener('click', () => abrirFormularioCliente(null));
        $('btnNovoProduto').addEventListener('click', () => abrirFormularioProduto(null));
        $('formCliente').addEventListener('submit', salvarCliente);
        $('formProduto').addEventListener('submit', salvarProduto);
        $('buscaClientes').addEventListener('input', renderizarClientes);
        $('buscaProdutos').addEventListener('input', renderizarProdutos);

        $('formPedido').addEventListener('submit', registrarPedido);
        $('btnAddItem').addEventListener('click', () => {
            estado.itensPedido.push({ produtoId: '', quantidade: 1 });
            renderizarItensPedido();
        });

        for (const gatilho of document.querySelectorAll('[data-fechar]')) {
            gatilho.addEventListener('click', fecharFolhas);
        }
        document.addEventListener('keydown', (e) => {
            if (e.key === 'Escape') fecharFolhas();
        });
    }

    async function iniciar() {
        ligarEventos();
        try {
            await api('/auth/me');
            await entrarNoApp();
        } catch (e) {
            // 401 = ainda não entrou (normal). Outros erros (ex.: sem internet) aparecem na tela de login.
            mostrarLogin(e instanceof ApiError && e.status !== 401 ? e.message : '');
        }
    }

    iniciar();
})();
