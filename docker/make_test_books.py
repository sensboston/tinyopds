#!/usr/bin/env python3
# Creates a small test library for the Docker image smoke test:
# loose FB2 files, a numbered ZIP archive with FB2 books, a duplicate and an invalid file
import os
import sys
import zipfile

FB2 = """<?xml version="1.0" encoding="utf-8"?>
<FictionBook xmlns="http://www.gribuser.ru/xml/fictionbook/2.0" xmlns:l="http://www.w3.org/1999/xlink">
<description>
<title-info>
<genre>sf</genre>
<author><first-name>{first}</first-name><last-name>{last}</last-name></author>
<book-title>{title}</book-title>
<annotation><p>{title}: test book</p></annotation>
<lang>ru</lang>
</title-info>
<document-info>
<author><nickname>test</nickname></author>
<date value="2020-01-01">2020</date>
<id>{id}</id>
<version>{version}</version>
</document-info>
</description>
<body><section><p>{title}</p></section></body>
</FictionBook>
"""

BOOKS = [
    # file name, first name, last name, title, id, version
    ("Пушкин - Капитанская дочка.fb2", "Александр", "Пушкин", "Капитанская дочка", "11111111-0000-0000-0000-000000000001", "1.0"),
    ("Гоголь - Шинель.fb2", "Николай", "Гоголь", "Шинель", "11111111-0000-0000-0000-000000000002", "1.0"),
]

ARCHIVE = [
    ("100001.fb2", "Антон", "Чехов", "Каштанка", "11111111-0000-0000-0000-000000000003", "1.0"),
    ("100002.fb2", "Иван", "Тургенев", "Муму", "11111111-0000-0000-0000-000000000004", "1.0"),
    # A newer copy of "Шинель": the archive copy is kept, the loose one is a duplicate
    ("100003.fb2", "Николай", "Гоголь", "Шинель", "11111111-0000-0000-0000-000000000005", "1.1"),
]


def fb2(first, last, title, book_id, version):
    return FB2.format(first=first, last=last, title=title, id=book_id, version=version).encode("utf-8")


def main(path):
    os.makedirs(path, exist_ok=True)
    for name, *book in BOOKS:
        with open(os.path.join(path, name), "wb") as f:
            f.write(fb2(*book))
    with zipfile.ZipFile(os.path.join(path, "fb2-100001-100003.zip"), "w", zipfile.ZIP_DEFLATED) as z:
        for name, *book in ARCHIVE:
            z.writestr(name, fb2(*book))
    with open(os.path.join(path, "broken.fb2"), "wb") as f:
        f.write(b"<?xml version='1.0'?><FictionBook>not a book")


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "books")
