# Perry Auth API

## Быстрый запуск витрины Perry — две иконки

В репозитории **[perry-front](https://github.com/ITSTEP-PERRY/perry-front)**:

| Ярлык | URL |
|-------|-----|
| **Perry Desktop** | http://localhost:3000 |
| **Perry Mobile** | http://localhost:8081 |

После clone фронта: `powershell -ExecutionPolicy Bypass -File .\Install-Perry-Shortcuts.ps1`.  
Mobile и Desktop ходят в этот Auth Service (login / register / forgot). Product API — `:5272`.

**Отчёт 01.10.2026 (mobile Expo + стыки):** [docs/ОТЧЁТ-2026-10-01.md](./docs/ОТЧЁТ-2026-10-01.md)

---

Backend-сервис клиентской части проекта **Perry**, отвечающий за аутентификацию и управление учётными записями пользователей.

## Deployment

Сервис контейнеризирован с помощью **Docker** и развернут в **Microsoft Azure Container Apps**.

**Production URL:**
https://perry-auth-service.orangeplant-910928aa.swedencentral.azurecontainerapps.io/

## API Documentation

Полное описание доступных API endpoints находится здесь:

[`docs/API.md`](docs/API.md)

## Документация дня

- [docs/ОТЧЁТ-2026-10-01.md](./docs/ОТЧЁТ-2026-10-01.md) — Mobile Figma 1:1, запуск Desktop/Mobile, стыки с Auth/Product
