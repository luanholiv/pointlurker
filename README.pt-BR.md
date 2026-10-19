# PointLurker

**Português (Brasil)** · [English](README.md)

[![Release](https://img.shields.io/github/v/release/luanholiv/pointlurker?sort=semver&logo=github)](https://github.com/luanholiv/pointlurker/releases/latest)
[![Backend image](https://img.shields.io/badge/GHCR-backend-2496ED?logo=docker&logoColor=white)](https://github.com/luanholiv/pointlurker/pkgs/container/pointlurker-backend)
[![Frontend image](https://img.shields.io/badge/GHCR-frontend-2496ED?logo=docker&logoColor=white)](https://github.com/luanholiv/pointlurker/pkgs/container/pointlurker-frontend)
[![License](https://img.shields.io/github/license/luanholiv/pointlurker)](LICENSE)

[![CI](https://img.shields.io/github/actions/workflow/status/luanholiv/pointlurker/ci.yml?branch=main&logo=github&label=CI)](https://github.com/luanholiv/pointlurker/actions/workflows/ci.yml)
[![Store API Probe](https://img.shields.io/github/actions/workflow/status/luanholiv/pointlurker/store-api-live-probe.yml?logo=github&label=store%20API%20probe)](https://github.com/luanholiv/pointlurker/actions/workflows/store-api-live-probe.yml)
[![codecov](https://img.shields.io/codecov/c/github/luanholiv/pointlurker?logo=codecov)](https://codecov.io/gh/luanholiv/pointlurker)
[![Known Vulnerabilities](https://snyk.io/test/github/luanholiv/pointlurker/badge.svg)](https://snyk.io/test/github/luanholiv/pointlurker)

Uma aplicação web auto-hospedada que monitora lojas de pontos de fidelidade — lojas de canais do [StreamElements](https://streamelements.com/) e a [GauPoints](https://gaupoints.com.br) —, avisa por e-mail quando um item da sua lista de desejos fica disponível e pode resgatá-lo automaticamente para você.

## Funcionalidades

- **Dois provedores de loja**: quantas lojas do StreamElements quiser, mais a loja GauPoints
- **Lista de desejos**: salve itens, com alerta por e-mail por item e resgate automático opcional
- **Resgate automático**: resgata quando o item está disponível, cabe no seu saldo e todos os campos exigidos estão preenchidos
- **Notificações por e-mail**: disponibilidade de itens, códigos de vale-presente devolvidos por um resgate e um aviso antes de um login de loja expirar
- **Conta única e segura**: senha mais 2FA via TOTP opcional; credenciais das lojas criptografadas em repouso
- **Bilíngue**: Inglês e Português (BR), inclusive nos e-mails de notificação
- **Tema claro/escuro**

## Início rápido

Execute a stack completa (proxy TLS, frontend, backend, PostgreSQL) com o Docker Compose:

```bash
cp .env.example .env    # depois defina os valores obrigatórios de SMTP__*
docker compose up -d
```

A aplicação é servida **somente via HTTPS** em `https://localhost` (defina `PUBLIC_HOST` para acessá-la de outros dispositivos), com um certificado da própria CA local do proxy; confie na raiz dela uma vez por dispositivo — [docs/DEPLOYMENT.md § HTTPS](docs/DEPLOYMENT.md#https). No primeiro acesso, você é guiado pela criação da sua conta.

O backend não inicia sem `SMTP__HOST`, `SMTP__USERNAME`, `SMTP__PASSWORD` e `SMTP__FROM`. Todas as variáveis: [docs/CONFIGURATION.md](docs/CONFIGURATION.md). Auto-hospedagem, configuração do Gmail e backups: [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md).

## Desenvolvimento

Pré-requisitos: o .NET SDK fixado em [`backend/global.json`](backend/global.json), Node.js 22+ e Docker (para o PostgreSQL e os testes de integração).

```bash
npm install                                    # raiz do repositório: git hooks + ferramentas dotnet
docker compose up database -d                  # somente o PostgreSQL

cd backend && dotnet run --project src/Presentation.WebApi    # terminal 1: https://localhost:8080

cd frontend && npm install && cp .env.example .env && npm run dev   # terminal 2: http://localhost:5173
```

Testes:

```bash
# a partir de backend/ — exclui as sondas ao vivo, que chamam as APIs reais dos provedores
dotnet test --filter "Category!=LiveProbe&Category!=LiveProbeWrite"

# a partir de frontend/
npm run test
```

Configuração completa: [docs/SETUP.md](docs/SETUP.md). Todos os comandos de build e teste: [docs/BUILD-AND-TEST.md](docs/BUILD-AND-TEST.md). Como funciona: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Licença

Este projeto é liberado em domínio público sob [a Unlicense](LICENSE).
