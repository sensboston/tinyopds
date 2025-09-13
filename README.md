# TinyOPDS

Легковесный и мощный OPDS сервер для домашних библиотек

![](https://github.com/sensboston/tinyopds/blob/master/wiki/Home_screen1-en.jpg?raw=true)

TinyOPDS - это простой и легковесный OPDS сервер, написанный на C#. Работает на Windows, Linux и macOS через .NET или Mono.

## TinyOPDS 3.0 - Революционное обновление!

**TinyOPDS 3.0** - результат интенсивной разработки, в ходе которой **переписано более 85% кода**. Главное изменение - переход от хранения данных в памяти к использованию **стандартного SQL движка**, что открывает путь к портированию на любые серверные платформы.

### Ключевые изменения версии 3.0:

- **SQLite база данных** - вместо загрузки всей библиотеки в память теперь используется стандартный SQL движок с полнотекстовым поиском FTS5
- **Современный веб-интерфейс** - полноценный встроенный веб-сайт с красивым дизайном, адаптивной вёрсткой и поддержкой пагинации для больших коллекций
- **Универсальная веб-читалка** - уникальный встроенный ридер для FB2 и EPUB файлов, доступный через браузер, с функциональностью на уровне специализированных приложений (темы, настройка шрифтов, закладки)
- **Революционный поиск** - уникальная комбинация русского Soundex и интеллектуальной транслитерации позволяет находить "Достоевский" по запросу "Dostoevsky" и наоборот, а также исправляет опечатки
- **Радикальное снижение потребления памяти** - с 1.6GB до 120-150MB для библиотеки из 500,000 книг (работает даже на Raspberry Pi Zero 2 W с 1GB RAM!)
- **Интеллектуальное сканирование** - автоматическое определение и удаление дубликатов с сохранением лучших версий
- **Переработанный HTTP сервер** - теперь эффективно обрабатывает параллельные запросы от множества клиентов
- **TinyOPDSCLI** - консольная версия переименована и модернизирована для современных стандартов
- **Простая установка** - добавлены графические инсталляторы для Linux и macOS, не требующие знания командной строки
- **Мультиязычная поддержка** - интерфейс локализован на 6 языков

### Реальные показатели:

| Метрика | Версия 2.x | Версия 3.0 |
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
- **100 MB RAM** минимум (благодаря SQLite)
- **10 MB** для приложения + место для базы данных книг

### Документация

Подробная документация, инструкции по установке и настройке доступны в [Wiki проекта](https://github.com/sensboston/tinyopds/wiki)

### Поддержка разработки

Если TinyOPDS помог организовать вашу библиотеку, можете поддержать разработку - ссылки на донаты находятся справа на странице проекта.

---

## TinyOPDS 3.0 - Revolutionary Update!

**TinyOPDS 3.0** - the result of intensive development with **over 85% of code rewritten**. The main change is the transition from in-memory storage to using a **standard SQL engine**, paving the way for porting to any server platform.

### Key changes in version 3.0:

- **SQLite database** - instead of loading the entire library into memory, now uses a standard SQL engine with FTS5 full-text search
- **Modern web interface** - fully functional embedded website with beautiful design, responsive layout, and pagination support for large collections
- **Universal web reader** - unique built-in reader for FB2 and EPUB files accessible through any browser, with functionality comparable to specialized reader apps (themes, font settings, bookmarks)
- **Revolutionary search** - unique combination of Russian Soundex and intelligent transliteration allows finding "Dostoevsky" by searching "Достоевский" and vice versa, plus typo correction
- **Dramatic memory reduction** - from 1.6GB to 120-150MB for a 500,000 book library (runs even on Raspberry Pi Zero 2 W with 1GB RAM!)
- **Smart scanning** - automatic duplicate detection and removal, keeping the best versions
- **Redesigned HTTP server** - now efficiently handles parallel requests from multiple clients
- **TinyOPDSCLI** - console version renamed and modernized to current standards
- **Easy installation** - added GUI installers for Linux and macOS, no command line knowledge required
- **Multi-language support** - interface localized in 6 languages

### Real performance metrics:

| Metric | Version 2.x | Version 3.0 |
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
- **100 MB RAM** minimum (thanks to SQLite)
- **10 MB** for application + space for book database

### Documentation

For detailed documentation, installation and configuration instructions, please visit the [Project Wiki](https://github.com/sensboston/tinyopds/wiki)

### Support Development

If TinyOPDS helped organize your library, you can support development - donation links are available on the right side of the project page.