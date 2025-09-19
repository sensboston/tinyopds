# TinyOPDS 3.4
Lightweight and powerful OPDS server for home libraries

![](https://github.com/sensboston/tinyopds/blob/master/wiki/ss1.png?raw=true)

TinyOPDS is a simple and lightweight OPDS server written in C#. Works on Windows, Linux and macOS via .NET or Mono.

## TinyOPDS 3.4 - Revolutionary Update!

**TinyOPDS 3.4** - the result of intensive development with **over 85% of code rewritten**. The main change is the transition from in-memory storage to using a **standard SQL engine**, paving the way for porting to any server platform.

### Key changes in version 3.4:

- **SQLite database** - instead of loading the entire library into memory, now uses a standard SQL engine with FTS5 full-text search
- **Modern web interface** - fully functional embedded website with beautiful design, responsive layout, and pagination support for large collections
- **Universal web reader** - unique built-in reader for FB2 and EPUB files accessible through any browser, with functionality comparable to specialized reader apps (themes, font settings, bookmarks)
- **Revolutionary search** - unique combination of Russian Soundex and intelligent transliteration allows finding "Dostoevsky" by searching "Достоевский" and vice versa, plus typo correction
- **Dramatic memory reduction** - from 1.6GB to 120-150MB for a 500,000 book library (but for smaller libraries (80000 books), app runs even on Raspberry Pi Zero 2 W with 1GB RAM!)
- **Smart scanning** - automatic duplicate detection and removal, keeping the best versions
- **Redesigned HTTP server** - now efficiently handles parallel requests from multiple clients
- **TinyOPDSCLI** - console version renamed and modernized to current standards
- **Easy installation** - CLI now has installer option for Linux and macOS, no command line knowledge required
- **Multi-language support** - interface (GUI and embedded website) localized in 6 languages

### Real performance metrics:

| Metric | Version 2.x | Version 3.x |
|--------|------------|------------|
| Architecture | All in memory | SQL engine |
| RAM for 500K books | ~1.6 GB | ~120-150 MB |
| Web interface | Basic | Modern with pagination |
| Book reader | No | FB2 + EPUB |
| Parallel requests | Limited | Up to 100 clients |
| Typo-tolerant search | No | Yes |
| Transliteration | No | Yes |
| Duplicates | Stored | Auto-removed |
| Installers | Windows | Windows, Linux, macOS |

### System Requirements

- **.NET Framework 4.8** (Windows) or **Mono 6.0+** (Linux/macOS)
- **2+ GB RAM** minimum (for large libraries 500000+ books)
- **10 MB** for application + **2GB** for the large library database

### Documentation

For detailed documentation, installation and configuration instructions, please visit the [Project Wiki](https://github.com/sensboston/tinyopds/wiki)

### Support Development

If TinyOPDS helped organize your library, you can support development - donation links are available on the right side of this page.

---

## TinyOPDS 3.4
Легкий та потужний OPDS сервер для домашніх бібліотек

TinyOPDS - це простий та легкий OPDS сервер, написаний на C#. Працює на Windows, Linux та macOS через .NET або Mono.

## TinyOPDS отримав революційне оновлення!

**TinyOPDS 3.4** - результат інтенсивної розробки, в ході якої **переписано понад 85% коду**. Головна зміна - перехід від зберігання даних в пам'яті до використання **стандартного SQL рушія**, що відкриває шлях до портування на будь-які серверні платформи.

### Ключові зміни версії 3.4:

- **SQLite база даних** - замість завантаження всієї бібліотеки в пам'ять тепер використовується стандартний SQL рушій з повнотекстовим пошуком FTS5
- **Сучасний веб-інтерфейс** - повноцінний вбудований веб-сайт з красивим дизайном, адаптивною версткою та підтримкою пагінації для великих колекцій
- **Універсальна веб-читалка** - унікальний вбудований рідер для FB2 та EPUB файлів, доступний через браузер, з функціональністю на рівні спеціалізованих додатків (теми, налаштування шрифтів, закладки)
- **Революційний пошук** - унікальна комбінація російського/англійського Soundex та інтелектуальної транслітерації дозволяє знаходити "Достоєвський" за запитом "Дастаєвський" або "Dostoevsky" і навпаки, а також виправляє друкарські помилки
- **Радикальне зниження споживання пам'яті** - з 1.6GB до 120-150MB для бібліотеки з 500,000 книг (з невеликою бібліотекою на 80000 книг, програма працює навіть на Raspberry Pi Zero 2 W з 1GB RAM!)
- **Інтелектуальне сканування** - автоматичне визначення та видалення дублікатів зі збереженням кращих версій
- **Перероблений HTTP сервер** - тепер ефективно обробляє паралельні запити від множини клієнтів
- **TinyOPDSCLI** - консольна версія перейменована та модернізована для сучасних стандартів
- **Проста установка** - в консольну версію додані інсталятори для Linux та macOS, що не потребують знання командного рядка
- **Багатомовна підтримка** - інтерфейс програми та вбудованого веб-сайту локалізовані для 6 мов

### Реальні показники:

| Метрика | Версія 2.x | Версія 3.x |
|---------|------------|------------|
| Архітектура | Все в пам'яті | SQL рушій |
| RAM для 500K книг | ~1.6 GB | ~120-150 MB |
| Веб-інтерфейс | Базовий | Сучасний з пагінацією |
| Читалка книг | Ні | FB2 + EPUB |
| Паралельні запити | Обмежено | До 100 клієнтів |
| Пошук з помилками | Ні | Є |
| Транслітерація | Ні | Є |
| Дублікати | Зберігаються | Автовидалення |
| Інсталятори | Windows | Windows, Linux, macOS |

### Системні вимоги

- **.NET Framework 4.8** (Windows) або **Mono 6.0+** (Linux/macOS)
- **2+ GB RAM** мінімум (для великих бібліотек на 500000 книг і більше)
- **10 MB** для додатку + **2GB** для бази даних книг великої бібліотеки

### Документація

Детальна документація, інструкції з установки та налаштування доступні в [Wiki проекту](https://github.com/sensboston/tinyopds/wiki)

### Підтримка розробки

Якщо TinyOPDS допоміг організувати вашу бібліотеку, можете підтримати розробку - посилання на донати знаходяться праворуч на сторінці.

---

## TinyOPDS 3.4
Легкий, но мощный OPDS сервер для домашних библиотек

TinyOPDS - это простой OPDS сервер, написанный на C#. Работает на Windows, Linux и macOS через .NET или Mono.

## TinyOPDS получил революционное обновление!

**TinyOPDS 3.4** - результат интенсивной разработки, в ходе которой **переписано более 85% кода**. Главное изменение - переход от хранения данных в памяти к использованию **стандартного SQL движка**, что открывает путь к портированию на любые серверные платформы.

### Ключевые изменения версии 3.4:

- **SQLite база данных** - вместо загрузки всей библиотеки в память теперь используется стандартный SQL движок с полнотекстовым поиском FTS5
- **Современный веб-интерфейс** - полноценный встроенный веб-сайт с красивым дизайном, адаптивной вёрсткой и поддержкой пагинации для больших коллекций
- **Универсальная веб-читалка** - уникальный встроенный ридер для FB2 и EPUB файлов, доступный через браузер, с функциональностью на уровне специализированных приложений (темы, настройка шрифтов, закладки)
- **Революционный поиск** - уникальная комбинация русского/английского Soundex и интеллектуальной транслитерации позволяет находить "Достоевский" по запросу "Дастаевский" или "Dostoevsky" и наоборот, а также исправляет опечатки
- **Радикальное снижение потребления памяти** - с 1.6GB до 120-150MB для библиотеки из 500,000 книг (с небольшой библиотекой на 80000 книг, программа работает даже на Raspberry Pi Zero 2 W с 1GB RAM!)
- **Интеллектуальное сканирование** - автоматическое определение и удаление дубликатов с сохранением лучших версий
- **Переработанный HTTP сервер** - теперь эффективно обрабатывает параллельные запросы от множества клиентов
- **TinyOPDSCLI** - консольная версия переименована и модернизирована для современных стандартов
- **Простая установка** - в консольную версию добавлены инсталляторы для Linux и macOS, не требующие знания командной строки
- **Мультиязычная поддержка** - интерфейс программы и встроенного веб-сайта локализованы для 6 языков

### Реальные показатели:

| Метрика | Версия 2.x | Версия 3.x |
|---------|------------|------------|
| Архитектура | Всё в памяти | SQL движок |
| RAM для 500K книг | ~1.6 GB | ~120-150 MB |
| Веб-интерфейс | Базовый | Современный с пагинацией |
| Читалка книг | Нет | FB2 + EPUB |
| Параллельные запросы | Ограниченно | До 100 клиентов |
| Поиск с опечатками | Нет | Есть |
| Транслитерация | Нет | Есть |
| Дубликаты | Хранятся | Автоудаление |
| Инсталляторы | Windows | Windows, Linux, macOS |

### Системные требования

- **.NET Framework 4.8** (Windows) или **Mono 6.0+** (Linux/macOS)
- **2+ GB RAM** минимум (для больших библиотек на 500000 книг и больше)
- **10 MB** для приложения + **2GB** для базы данных книг большой библиотеки

### Документация

Подробная документация, инструкции по установке и настройке доступны в [Wiki проекта](https://github.com/sensboston/tinyopds/wiki)

### Поддержка разработки

Если TinyOPDS помог организовать вашу библиотеку, можете поддержать разработку - ссылки на донаты находятся справа на странице.
