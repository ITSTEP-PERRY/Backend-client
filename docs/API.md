# Perry Auth API

API-сервіс для автентифікації, авторизації та керування обліковими записами користувачів у системі **Perry**.

Сервіс відповідає за реєстрацію користувачів, підтвердження електронної пошти, авторизацію, керування access/refresh токенами, відновлення пароля, ролі та статуси користувачів, а також надає захищений API для взаємодії з іншими backend-сервісами Perry.

**API:**  
https://perry-auth-service.orangeplant-910928aa.swedencentral.azurecontainerapps.io

**Swagger:**  
https://perry-auth-service.orangeplant-910928aa.swedencentral.azurecontainerapps.io/swagger/index.html

## Автентифікація

| Метод | Endpoint | Опис |
| --- | --- | --- |
| `POST` | `/api/auth/register` | Розпочинає реєстрацію нового користувача та ініціює підтвердження електронної пошти. |
| `POST` | `/api/auth/verify-email` | Перевіряє код підтвердження та підтверджує електронну пошту користувача. |
| `POST` | `/api/auth/resend-verification-code` | Генерує та повторно надсилає код підтвердження електронної пошти. |
| `POST` | `/api/auth/complete-registration` | Завершує реєстрацію після підтвердження email та зберігає дані користувача. |
| `POST` | `/api/auth/login` | Авторизує користувача та повертає access JWT і refresh token. |
| `POST` | `/api/auth/refresh` | Видає новий access token за допомогою чинного refresh token. |
| `POST` | `/api/auth/logout` | Завершує поточну refresh-сесію. |
| `GET` | `/api/auth/me` | Повертає інформацію про поточного авторизованого користувача. |
| `POST` | `/api/auth/forgot-password` | Запускає процес відновлення пароля та надсилає код підтвердження. |
| `POST` | `/api/auth/reset-password` | Перевіряє код відновлення та встановлює новий пароль. |

## Керування користувачами

Адміністративні endpoints доступні лише авторизованим користувачам із роллю `Admin`.

| Метод | Endpoint | Опис |
| --- | --- | --- |
| `GET` | `/api/admin/users` | Повертає список користувачів. |
| `GET` | `/api/admin/users/{id}` | Повертає інформацію про користувача за його ID. |
| `PATCH` | `/api/admin/users/{id}/role` | Змінює роль користувача. |
| `PATCH` | `/api/admin/users/{id}/status` | Змінює статус користувача. |

### Ролі користувачів

Підтримуються такі ролі:

- `User` — звичайний користувач системи.
- `Admin` — користувач із правами керування обліковими записами.

### Статуси користувачів

Підтримуються такі статуси:

- `Active` — активний обліковий запис.
- `Deleted` — обліковий запис позначений як видалений.

## Internal API

Internal API призначений для захищеної взаємодії між backend-сервісами Perry.

Для доступу використовується окремий Internal JWT. Сервіс, що викликає AuthService, повинен пройти service-to-service автентифікацію та мати необхідні permissions.

| Метод | Endpoint | Опис |
| --- | --- | --- |
| `GET` | `/internal/users` | Повертає список користувачів для внутрішніх backend-сервісів. |
| `GET` | `/internal/users/{id}` | Повертає інформацію про конкретного користувача за його ID. |
| `PATCH` | `/internal/users/{id}/role` | Змінює роль користувача через внутрішній API. |
| `PATCH` | `/internal/users/{id}/status` | Змінює статус користувача через внутрішній API. |

### Permissions

Для Internal API використовуються такі permissions:

- `users.read` — дозволяє отримувати інформацію про користувачів.
- `users.manage` — дозволяє змінювати роль та статус користувачів.

Internal JWT передається через HTTP-заголовок:

```http
Authorization: Bearer <internal_access_token>
```

> Internal JWT, service credentials, credential hashes та signing secrets призначені виключно для backend-сервісів і не повинні передаватися frontend-клієнтам або зберігатися у репозиторії.

## Health checks

| Метод | Endpoint | Опис |
| --- | --- | --- |
| `GET` | `/api/health` | Перевіряє, чи запущений API та чи може сервіс обробляти запити. |
| `GET` | `/api/health/database` | Перевіряє доступність PostgreSQL та підключення сервісу до бази даних. |

## Авторизація користувачів

Після успішного входу AuthService видає access JWT.

Для захищених користувацьких endpoints токен передається через HTTP-заголовок:

```http
Authorization: Bearer <access_token>
```

Ідентифікатор користувача передається в JWT та може використовуватися іншими сервісами Perry для прив'язки власних даних до конкретного користувача.

## Service-to-service авторизація

Backend-сервіси не повинні використовувати облікові дані звичайного користувача для внутрішньої взаємодії.

Для service-to-service запитів AuthService використовує окремі service credentials та Internal JWT із відповідними permissions.

Frontend не повинен мати доступу до:

- service credentials;
- Internal JWT signing secret;
- credential hashes;
- інших backend secrets.

## Конфігурація

Секретні значення не повинні зберігатися у Git.

Для локальної розробки використовуються .NET User Secrets, а для production — secrets та environment variables середовища розгортання.

До конфігурації Internal JWT належать, зокрема:

```text
InternalJwt__SigningSecret
InternalJwt__Services__0__Name
InternalJwt__Services__0__CredentialHash
InternalJwt__Services__0__Permissions__0
InternalJwt__Services__0__Permissions__1
```

## Deployment

Production-версія AuthService запускається як Docker-контейнер.

Docker image:

```text
ghcr.io/itstep-perry/perry-auth-service
```

Production-середовище працює в Azure Container Apps.
