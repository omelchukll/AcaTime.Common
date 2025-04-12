# AcaTime.Common

Містить спільні компоненти для системи планування розкладу занять AcaTime.

## Структура

Підмодуль містить такі проекти:

- **AcaTime.ScheduleCommon** - Загальні утиліти та моделі для роботи з розкладом
- **AcaTime.ScriptModels** - Моделі для роботи зі скриптами генерації розкладу
- **AcaTime.Algorithm.Default** - Реалізація алгоритму генерації розкладу за замовчуванням
- **AcaTime.ScheduleGenerator** - Генератор розкладу занять

## Швидке розгортання серверної частини

Для швидкого запуску серверної частини AcaTime можна використати готовий Docker образ.
Детальні інструкції знаходяться в [Docker/Server/README.md](Docker/Server/README.md).

## Розробка алгоритмів генерації розкладу

Система AcaTime підтримує можливість розробки та підключення власних алгоритмів генерації розкладу. Для створення нового алгоритму необхідно:

1. Створити новий проект в рішенні `AcaTime.Common.sln` у форматі `AcaTime.Algorithm.{YourAlgorithmName}`
2. Реалізувати інтерфейс `IScheduleAlgorithm` з проекту `AcaTime.ScheduleCommon`
3. Зареєструвати алгоритм у проекті `AcaTime.ScheduleGenerator`
4. Налаштувати параметри алгоритму в `appsettings.json`

Детальну інструкцію з розробки алгоритмів можна знайти в [AcaTime.ScheduleGenerator/README.md](AcaTime.ScheduleGenerator/README.md).

