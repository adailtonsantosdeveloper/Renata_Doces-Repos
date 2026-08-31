const API_URL = "https://localhost:7260/api";

function switchTab(tabId, event) {
    document.querySelectorAll('.tab-content').forEach(el => el.classList.remove('active'));
    document.querySelectorAll('.tab-btn').forEach(el => el.classList.remove('active'));
    document.getElementById(tabId).classList.add('active');

    if (event && event.currentTarget) {
        event.currentTarget.classList.add('active');
    }

    if (tabId === 'clientes') carregarClientes();
    if (tabId === 'produtos') carregarProdutos();
    if (tabId === 'pedidos') {
        carregarClientesSelect();
        carregarProdutosSelect();
        carregarPedidos();
    }
}

// --- CLIENTES ---
async function carregarClientes() {
    try {
        const res = await fetch(`${API_URL}/Clientes`);
        const dados = await res.json();
        const tbody = document.getElementById('tabelaClientes');

        if (dados.length === 0) {
            tbody.innerHTML = `<tr><td colspan="4" style="text-align: center; color: #6c757d;">Nenhum cliente cadastrado.</td></tr>`;
            return;
        }

        // Adicionamos um evento de clique na linha (tr) inteira para carregar os dados no formulário
        tbody.innerHTML = dados.map(c => `
            <tr onclick="selecionarClienteParaEdicao(${c.id})" style="cursor: pointer;" title="Clique para editar">
                <td>${c.id}</td>
                <td>${c.nome}</td>
                <td>${c.telefone || '-'}</td>
                <td>${c.email || '-'}</td>
            </tr>
        `).join('');
    } catch (error) {
        console.error("Erro ao carregar clientes:", error);
    }
}

async function salvarCliente(e) {
    e.preventDefault();
    const id = document.getElementById('clienteId').value;
    const cliente = {
        id: id ? parseInt(id) : 0,
        nome: document.getElementById('clienteNome').value,
        telefone: document.getElementById('clienteTelefone').value,
        email: document.getElementById('clienteEmail').value,
        endereco: document.getElementById('clienteEndereco').value
    };

    try {
        if (id) {
            // Se tem ID, é uma edição (PUT)
            await fetch(`${API_URL}/Clientes/${id}`, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(cliente)
            });
            alert('Cliente alterado com sucesso!');
        } else {
            // Se não tem ID, é um cadastro novo (POST)
            await fetch(`${API_URL}/Clientes`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(cliente)
            });
            alert('Cliente cadastrado com sucesso!');
        }
        limparFormularioCliente();
        carregarClientes();
    } catch (error) {
        console.error("Erro ao salvar cliente:", error);
        alert('Erro ao salvar cliente.');
    }
}

async function selecionarClienteParaEdicao(id) {
    try {
        const res = await fetch(`${API_URL}/Clientes/${id}`);
        const c = await res.json();

        // Joga os dados de volta para o formulário
        document.getElementById('clienteId').value = c.id;
        document.getElementById('clienteNome').value = c.nome;
        document.getElementById('clienteTelefone').value = c.telefone || '';
        document.getElementById('clienteEmail').value = c.email || '';
        document.getElementById('clienteEndereco').value = c.endereco || '';

        // Muda o botão para "Salvar" e exibe o botão de cancelar
        const btnSalvar = document.getElementById('btnSalvarCliente');
        btnSalvar.textContent = 'Salvar';
        btnSalvar.style.backgroundColor = '#0d6efd'; // Cor azul para indicar edição
        document.getElementById('btnCancelarCliente').style.display = 'block';

        // Rola a tela suavemente para o formulário no topo
        window.scrollTo({ top: 0, behavior: 'smooth' });
    } catch (error) {
        console.error("Erro ao selecionar cliente:", error);
    }
}

function limparFormularioCliente() {
    document.getElementById('clienteForm').reset();
    document.getElementById('clienteId').value = '';

    const btnSalvar = document.getElementById('btnSalvarCliente');
    btnSalvar.textContent = 'Cadastrar Cliente';
    btnSalvar.style.backgroundColor = ''; // Volta à cor original do CSS
    document.getElementById('btnCancelarCliente').style.display = 'none';
}


// --- PRODUTOS ---
async function carregarProdutos() {
    try {
        const res = await fetch(`${API_URL}/Produtos`);
        const dados = await res.json();
        const tbody = document.getElementById('tabelaProdutos');

        if (dados.length === 0) {
            tbody.innerHTML = `<tr><td colspan="5" style="text-align: center; color: #6c757d;">Nenhum produto cadastrado.</td></tr>`;
            return;
        }

        tbody.innerHTML = dados.map(p => `
            <tr onclick="selecionarProdutoParaEdicao(${p.id})" style="cursor: pointer;" title="Clique para editar">
                <td>${p.id}</td>
                <td>${p.codigo}</td>
                <td>${p.nome}</td>
                <td>${p.sabor || '-'}</td>
                <td>R$ ${p.precoVenda.toFixed(2)}</td>
            </tr>
        `).join('');
    } catch (error) {
        console.error("Erro ao carregar produtos:", error);
    }
}

async function salvarProduto(e) {
    e.preventDefault();
    const id = document.getElementById('produtoDatabaseId').value;
    const produto = {
        id: id ? parseInt(id) : 0,
        codigo: document.getElementById('produtoCodigo').value,
        nome: document.getElementById('produtoNome').value,
        sabor: document.getElementById('produtoSabor').value,
        recheio: document.getElementById('produtoRecheio').value,
        peso: document.getElementById('produtoPeso').value,
        precoVenda: parseFloat(document.getElementById('produtoPreco').value),
        ingredientes: document.getElementById('produtoIngredientes').value
    };

    try {
        if (id) {
            await fetch(`${API_URL}/Produtos/${id}`, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(produto)
            });
            alert('Produto alterado com sucesso!');
        } else {
            await fetch(`${API_URL}/Produtos`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(produto)
            });
            alert('Produto cadastrado com sucesso!');
        }
        limparFormularioProduto();
        carregarProdutos();
    } catch (error) {
        console.error("Erro ao salvar produto:", error);
        alert('Erro ao salvar produto.');
    }
}

async function selecionarProdutoParaEdicao(id) {
    try {
        const res = await fetch(`${API_URL}/Produtos/${id}`);
        const p = await res.json();

        document.getElementById('produtoDatabaseId').value = p.id;
        document.getElementById('produtoCodigo').value = p.codigo;
        document.getElementById('produtoNome').value = p.nome;
        document.getElementById('produtoSabor').value = p.sabor || '';
        document.getElementById('produtoRecheio').value = p.recheio || '';
        document.getElementById('produtoPeso').value = p.peso || '';
        document.getElementById('produtoPreco').value = p.precoVenda;
        document.getElementById('produtoIngredientes').value = p.ingredientes || '';

        const btnSalvar = document.getElementById('btnSalvarProduto');
        btnSalvar.textContent = 'Salvar';
        btnSalvar.style.backgroundColor = '#0d6efd';
        document.getElementById('btnCancelarProduto').style.display = 'block';

        window.scrollTo({ top: 0, behavior: 'smooth' });
    } catch (error) {
        console.error("Erro ao selecionar produto:", error);
    }
}

function limparFormularioProduto() {
    document.getElementById('produtoForm').reset();
    document.getElementById('produtoDatabaseId').value = '';

    const btnSalvar = document.getElementById('btnSalvarProduto');
    btnSalvar.textContent = 'Cadastrar Produto';
    btnSalvar.style.backgroundColor = '';
    document.getElementById('btnCancelarProduto').style.display = 'none';
}


// --- PEDIDOS ---
async function carregarClientesSelect() {
    const res = await fetch(`${API_URL}/Clientes`);
    const dados = await res.json();
    document.getElementById('pedidoClienteSelect').innerHTML = dados.map(c => `<option value="${c.id}">${c.nome}</option>`).join('');
}

async function carregarProdutosSelect() {
    const res = await fetch(`${API_URL}/Produtos`);
    const dados = await res.json();
    document.getElementById('pedidoProdutoSelect').innerHTML = dados.map(p => `<option value="${p.id}">${p.nome} - R$ ${p.precoVenda.toFixed(2)}</option>`).join('');
}

async function salvarPedido(e) {
    e.preventDefault();
    const pedidoDto = {
        clienteId: parseInt(document.getElementById('pedidoClienteSelect').value),
        itens: [
            {
                produtoId: parseInt(document.getElementById('pedidoProdutoSelect').value),
                quantidade: parseInt(document.getElementById('pedidoQuantidade').value)
            }
        ]
    };

    await fetch(`${API_URL}/Pedidos`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(pedidoDto)
    });
    carregarPedidos();
    alert('Pedido registrado com sucesso!');
}

async function carregarPedidos() {
    const res = await fetch(`${API_URL}/Pedidos`);
    const dados = await res.json();
    document.getElementById('tabelaPedidos').innerHTML = dados.map(p => `
        <tr>
            <td>${p.id}</td>
            <td>${p.cliente ? p.cliente.nome : 'Cliente #' + p.clienteId}</td>
            <td>${new Date(p.dataPedido).toLocaleDateString()}</td>
            <td>R$ ${p.valorTotal.toFixed(2)}</td>
        </tr>
    `).join('');
}

// Inicialização
carregarClientes();