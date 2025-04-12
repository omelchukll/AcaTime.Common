# AcaTime - Швидкий запуск серверної частини

## Важлива інформація
- Репозиторій образу `ghcr.io/omelchukll/acatime-api` є **приватним**
- Для доступу потрібен акаунт GitHub з наданим доступом
- Необхідно авторизуватися в GitHub Container Registry перед запуском

## Підготовка до запуску

### 1. Авторизація в GitHub Container Registry
Для доступу до приватного репозиторію необхідно авторизуватися:

```bash
# Логін до GitHub Container Registry
echo $GITHUB_PAT | docker login ghcr.io -u USERNAME --password-stdin
```

Де:
- `USERNAME` - ваш логін на GitHub
- `$GITHUB_PAT` - Personal Access Token з дозволами `read:packages`

#### Створення GitHub Personal Access Token:
1. Перейдіть в налаштування: https://github.com/settings/tokens
2. Створіть токен з правами `read:packages`
3. Збережіть токен у безпечному місці

### 2. Запуск додатку з Docker Compose

```bash
# Перейдіть в директорію з docker-compose.yml
cd AcaTime.Common/Docker/Server

# Запустіть контейнери
docker-compose up -d
```

### 3. Перевірка статусу

```bash
docker-compose ps
```

API сервіс буде доступний за адресою: http://localhost:5000

## Тестові дані та облікові записи

Образ містить тестові дані, які дозволяють одразу почати роботу з системою:

### Облікові записи користувачів:

1. **Для роботи з тестовим факультетом:**
   - Логін: `test`
   - Пароль: `qwerty`
   - Права: Перегляд та управління тестовим факультетом

2. **Для адміністрування користувачів та факультетів:**
   - Логін: `admin`
   - Пароль: `admin`
   - Права: Доступ до адміністрування системи

## Вимкнення додатку

```bash
docker-compose down
```

Для видалення даних (включаючи бази даних):

```bash
docker-compose down -v
```

## Примітки
- Переконайтеся, що ваш GitHub акаунт має доступ до репозиторію
- При помилці `unauthorized: authentication required` перевірте токен
- Для отримання доступу зверніться до адміністратора репозиторію

## Структура файлів
- `docker-compose.yml` - основний файл конфігурації для запуску всієї системи
- `DB/sample.sql` - скрипт ініціалізації бази даних PostgreSQL 