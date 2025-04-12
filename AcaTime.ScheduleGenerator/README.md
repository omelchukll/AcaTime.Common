# AcaTime.ScheduleGenerator

Консольний додаток для генерації розкладу занять у системі AcaTime за допомогою підключених алгоритмів.

## Призначення

Проект призначений для запуску та керування алгоритмами генерації розкладу. Додаток:
- Отримує дані про факультет, групи, викладачів та предмети з API AcaTime
- Запускає вибраний алгоритм генерації розкладу
- Повертає результати роботи алгоритму в API для подальшого використання

## Конфігурація

Налаштування додатку знаходяться у файлі `appsettings.json`:

```json
{
  "ServerUrl": "http://localhost:5000/",
  "ApiKey": "_API_KEY_",
  "AlgorithmName": "Default",
  "IgnoreClassrooms": 0,
  "Algorithms": {
    "Default": {
      "ResultsCount": 2,
      "MaxIterations": 100,
      "TimeoutInSeconds": 600,
      "SlotsTopK": 3,
      "DomainsTopK": 1,
      "SlotsTemperature": 1,
      "DomainsTemperature": 1
    }
  }
}
```

Де:
- `ServerUrl` - URL сервера AcaTime API
- `ApiKey` - ключ для авторизації в API
- `AlgorithmName` - назва алгоритму, який буде використовуватися
- `IgnoreClassrooms` - ігнорувати обмеження аудиторій (0 - ні, 1 - так)
- `Algorithms` - секція з параметрами для кожного з алгоритмів

## Створення нового алгоритму генерації розкладу

Нижче наведена покрокова інструкція для створення та інтеграції нового алгоритму генерації розкладу.

### Крок 1: Створення нового проекту

1. Додайте новий проект класів до рішення `AcaTime.Common.sln`:
   ```
   AcaTime.Algorithm.{YourAlgorithmName}
   ```

2. Додайте посилання на необхідні проекти:
   - `AcaTime.ScheduleCommon`
   - `AcaTime.ScriptModels` (якщо потрібно)

### Крок 2: Реалізація інтерфейсу IScheduleAlgorithm

Створіть головний клас вашого алгоритму, який реалізує інтерфейс `IScheduleAlgorithm`:

```csharp
using AcaTime.ScheduleCommon.Abstract;
using AcaTime.ScheduleCommon.Models.Calc;
using AcaTime.ScheduleCommon.Models.Constraints;
using Microsoft.Extensions.Logging;

namespace AcaTime.Algorithm.YourAlgorithmName
{
    public class YourScheduleAlgorithm : IScheduleAlgorithm
    {
        // Реалізація методів інтерфейсу

        public async Task<List<AlgorithmResultDTO>> Run(
            FacultySeasonDTO root, 
            UserFunctions userFunctions, 
            Dictionary<string, string> parameters, 
            bool ignoreClassrooms, 
            ILogger logger, 
            CancellationToken cancellationToken = default)
        {
            // Ваша логіка генерації розкладу
            // ...
            return new List<AlgorithmResultDTO>();
        }

        public string GetStatistics()
        {
            // Повертає статистику роботи алгоритму
            return "Статистика роботи алгоритму YourAlgorithm";
        }

        public string GetName()
        {
            // Назва алгоритму - коротка, англійською
            return "YourAlgorithm";
        }

        public List<AlgorithmParameterDTO> GetParameters()
        {
            // Список параметрів, які використовує алгоритм
            return new List<AlgorithmParameterDTO>
            {
                new AlgorithmParameterDTO
                {
                    Name = "ParameterName",
                    Description = "Опис параметра",
                    Type = "int",
                    DefaultValue = "10"
                }
                // Додайте інші параметри
            };
        }
    }
}
```

#### Опис методів інтерфейсу

1. **Run** - Головний метод алгоритму, який запускає генерацію розкладу:
   - `root` - Дані про факультет, семестр, групи, викладачів, предмети
   - `userFunctions` - Функції користувача для обмежень та оцінки
   - `parameters` - Параметри алгоритму з конфігурації
   - `ignoreClassrooms` - Флаг, що вказує чи ігнорувати обмеження аудиторій
   - `logger` - Логер для запису інформації про роботу алгоритму
   - `cancellationToken` - Токен скасування для зупинки алгоритму

2. **GetStatistics** - Повертає довільний текст зі статистикою роботи алгоритму

3. **GetName** - Повертає унікальну назву алгоритму (англійською, коротко)

4. **GetParameters** - Повертає список параметрів, які використовує алгоритм:
   - `Name` - Назва параметра (ключ в словнику `parameters`)
   - `Description` - Опис параметра для користувача
   - `Type` - Тип параметра (int, float, bool, string)
   - `DefaultValue` - Значення за замовчуванням

### Крок 3: Реєстрація алгоритму в проекті ScheduleGenerator

1. Додайте посилання на ваш проект у `AcaTime.ScheduleGenerator.csproj`:

2. Зареєструйте ваш алгоритм у класі `Program.cs` в методі `ConfigureServices`:

```csharp
private static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
{
    // ... інші сервіси ...
    
    // Реєстрація алгоритму (як синглтон)
    services.AddSingleton<IScheduleAlgorithm, YourScheduleAlgorithm>();
    
    // ... інші сервіси ...
}
```

### Крок 4: Додавання параметрів алгоритму в конфігурацію

Оновіть файл `appsettings.json`, додавши секцію з параметрами вашого алгоритму:

```json
{
  "ServerUrl": "http://localhost:5000/",
  "ApiKey": "_API_KEY_",
  "AlgorithmName": "YourAlgorithm",
  "IgnoreClassrooms": 0,
  "Algorithms": {
    "Default": {
      "ResultsCount": 2,
      "MaxIterations": 100,
      "TimeoutInSeconds": 600,
      "SlotsTopK": 3,
      "DomainsTopK": 1,
      "SlotsTemperature": 1,
      "DomainsTemperature": 1
    },
    "YourAlgorithm": {
      "ParameterName": 10,
      "OtherParameter": 50
    }
  }
}
```

Зверніть увагу на зміну значення `AlgorithmName` на назву вашого алгоритму, яку повертає метод `GetName()`.

## Принцип роботи генератора розкладу

Процес генерації розкладу складається з наступних кроків:

1. Отримання даних про факультет і семестр з API AcaTime
2. Вибір потрібного алгоритму з зареєстрованих у DI-контейнері
3. Передача даних в алгоритм і запуск його виконання
4. Отримання результатів роботи алгоритму
5. Відправка результатів назад в API AcaTime

## Вимоги до алгоритмів

- Алгоритм повинен бути потокобезпечним
- Алгоритм повинен підтримувати скасування через CancellationToken


## Запуск генератора

Для запуску генератора розкладу використовуйте команду:

```bash
dotnet run --project AcaTime.ScheduleGenerator
```

або запустіть скомпільований виконуваний файл:

```bash
./AcaTime.ScheduleGenerator
```

або з Visual Studio