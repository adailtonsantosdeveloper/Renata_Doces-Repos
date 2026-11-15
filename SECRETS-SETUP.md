# 🔐 GUIA PASSO A PASSO - CONFIGURAR SECRETS NO GITHUB

## 📍 PASSO 1: Acesse as Configurações do Repositório

1. Acesse seu repositório: https://github.com/adailtonsantosdeveloper/Renata_Doces-Repos
2. Clique em **Settings** (engrenagem no topo)
3. No menu lateral esquerdo, clique em **Secrets and variables**
4. Clique em **Actions**

Você verá a opção: **New repository secret**

---

## 🔧 ESCOLHA SUA HOSPEDAGEM

### OPÇÃO A: AZURE CONTAINER INSTANCES (Recomendado)

Se usar Azure, você precisa de:

#### 1️⃣ REGISTRY_URL
**O que é:** URL do seu Azure Container Registry

**Como obter:**
```bash
az acr show --name seu-registry --query loginServer -o tsv
```

**Exemplo:** `seu-registry.azurecr.io`

**Para adicionar no GitHub:**
- Name: `REGISTRY_URL`
- Value: `seu-registry.azurecr.io`
- Clique em **Add secret**

---

#### 2️⃣ REGISTRY_USERNAME
**O que é:** Usuário do Container Registry

**Como obter:**
```bash
az acr credential show --name seu-registry --query username -o tsv
```

**Para adicionar no GitHub:**
- Name: `REGISTRY_USERNAME`
- Value: `seu-usuario`
- Clique em **Add secret**

---

#### 3️⃣ REGISTRY_PASSWORD
**O que é:** Senha do Container Registry

**Como obter:**
```bash
az acr credential show --name seu-registry --query "passwords[0].value" -o tsv
```

**Para adicionar no GitHub:**
- Name: `REGISTRY_PASSWORD`
- Value: `SuaSenhaLonga`
- Clique em **Add secret**

---

#### 4️⃣ AZURE_CREDENTIALS (O mais importante!)
**O que é:** Chave de autenticação para Azure (formato JSON)

**Como obter:**
```bash
az ad sp create-for-rbac --name "RenataDoces-CI" \
  --role contributor \
  --scopes /subscriptions/{seu-subscription-id}/resourceGroups/{seu-resource-group} \
  --json-auth
```

Será retornado algo como:
```json
{
  "clientId": "xxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxx",
  "clientSecret": "xxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxx",
  "subscriptionId": "xxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxx",
  "tenantId": "xxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxx",
  "activeDirectoryEndpointUrl": "https://login.microsoftonline.com",
  "resourceManagerEndpointUrl": "https://management.azure.com/",
  "activeDirectoryGraphResourceId": "https://graph.windows.net/",
  "sqlManagementEndpointUrl": "https://management.core.windows.net:8443/",
  "galleryEndpointUrl": "https://gallery.azure.com/",
  "managementEndpointUrl": "https://management.core.windows.net/"
}
```

**Para adicionar no GitHub:**
- Name: `AZURE_CREDENTIALS`
- Value: Cole **todo o JSON acima** (com quebras de linha)
- Clique em **Add secret**

---

#### 5️⃣ AZURE_RESOURCE_GROUP
**O que é:** Nome do seu Resource Group no Azure

**Como obter:**
```bash
az group list --query "[].name" -o tsv
```

**Exemplo:** `seu-resource-group`

**Para adicionar no GitHub:**
- Name: `AZURE_RESOURCE_GROUP`
- Value: `seu-resource-group`
- Clique em **Add secret**

---

#### 6️⃣ DB_CONNECTION_STRING
**O que é:** String de conexão do SQL Server

**Formato para Azure SQL:**
```
Server=seu-servidor.database.windows.net,1433;Database=RenataDoces;User Id=admin@seu-servidor;Password=SenhaForteMuitoSegura123!;Encrypt=true;Connection Timeout=30;
```

**Como obter no Azure:**
```bash
az sql server show --resource-group seu-rg --name seu-servidor --query fullyQualifiedDomainName -o tsv
```

**Para adicionar no GitHub:**
- Name: `DB_CONNECTION_STRING`
- Value: Cole a string acima (com sua senha)
- Clique em **Add secret**

---

#### 7️⃣ AUTH_USUARIO
**O que é:** Usuário admin inicial da aplicação

**Exemplo:** `admin`

**Para adicionar no GitHub:**
- Name: `AUTH_USUARIO`
- Value: `admin`
- Clique em **Add secret**

---

#### 8️⃣ AUTH_SENHA
**O que é:** Senha admin inicial (mínimo 10 caracteres)

**Exemplo:** `SenhaForteMuitoSegura123!`

**Para adicionar no GitHub:**
- Name: `AUTH_SENHA`
- Value: `SenhaForteMuitoSegura123!`
- Clique em **Add secret**

---

### OPÇÃO B: RENDER.COM (Mais Simples)

Se usar Render em vez de Azure:

#### 1️⃣ RENDER_DEPLOY_WEBHOOK
**O que é:** URL para dispara deploy automático

**Como obter:**
1. Acesse seu serviço em https://dashboard.render.com
2. Clique em **Settings** (no seu serviço)
3. Procure por **Deploy Hook** ou **API Token**
4. Copie a URL do webhook

**Para adicionar no GitHub:**
- Name: `RENDER_DEPLOY_WEBHOOK`
- Value: `https://api.render.com/deploy/srv-...`
- Clique em **Add secret**

---

#### 2️⃣ RENDER_SERVICE_ID
**O que é:** ID do seu serviço no Render

**Como obter:**
Na URL de seu serviço: `https://dashboard.render.com/services/srv-xxxxx`

**Para adicionar no GitHub:**
- Name: `RENDER_SERVICE_ID`
- Value: `srv-xxxxx`
- Clique em **Add secret**

---

#### 3️⃣ RENDER_SERVICE_URL
**O que é:** URL pública de sua aplicação

**Exemplo:** `https://renata-doces.onrender.com`

**Para adicionar no GitHub:**
- Name: `RENDER_SERVICE_URL`
- Value: `https://renata-doces.onrender.com`
- Clique em **Add secret**

---

## ✅ VERIFICAR SECRETS ADICIONADOS

Após adicionar todos, você deve ver uma lista assim:

```
🔒 REGISTRY_URL
🔒 REGISTRY_USERNAME
🔒 REGISTRY_PASSWORD
🔒 AZURE_CREDENTIALS
🔒 AZURE_RESOURCE_GROUP
🔒 DB_CONNECTION_STRING
🔒 AUTH_USUARIO
🔒 AUTH_SENHA
```

Clique em cada um para verificar se foi adicionado (não mostra o valor por segurança).

---

## 🚀 APÓS CONFIGURAR OS SECRETS

Faça um novo commit para disparar o CI/CD:

```bash
git commit --allow-empty -m "trigger: dispara CI/CD com secrets configurados"
git push origin main
```

Depois acesse: https://github.com/adailtonsantosdeveloper/Renata_Doces-Repos/actions

Você deve ver os workflows rodando:
- ✅ build.yml → Build e push da imagem
- ✅ deploy-azure.yml (ou deploy-render.yml) → Deploy automático

---

## 🆘 TROUBLESHOOTING

### Workflow falha com "Permission denied"
- Verifique se o `AZURE_CREDENTIALS` JSON está completo
- Confirme que o Service Principal tem permissões

### "Registry authentication failed"
- Verifique `REGISTRY_URL`, `REGISTRY_USERNAME`, `REGISTRY_PASSWORD`
- Teste localmente:
  ```bash
  docker login seu-registry.azurecr.io -u seu-usuario -p sua-senha
  ```

### "Container already exists"
- Remova o container antigo:
  ```bash
  az container delete --resource-group seu-rg --name renata-doces-app
  ```

### Deploy não dispara após push
- Verifique em "Actions" se há erros
- Confirme que os secrets estão no lugar certo (Settings → Secrets)
- Reinicie o workflow manualmente

---

## 🔐 BOAS PRÁTICAS

✅ **Faça:**
- Use senhas fortes (12+ caracteres, maiúsculas, números, símbolos)
- Rotacione credenciais mensalmente
- Use `--role contributor` apenas quando necessário

❌ **NÃO Faça:**
- Nunca commite secrets no repositório
- Não compartilhe URLs de webhook
- Não use a mesma senha para diferentes serviços

---

## 📞 PRÓXIMOS PASSOS

1. ✅ Configure todos os secrets acima
2. ✅ Faça um push para testar
3. ✅ Acompanhe em "Actions"
4. ✅ Aplicação deve estar em produção automaticamente!

**Seu CI/CD está pronto para deploy automático!** 🎉
