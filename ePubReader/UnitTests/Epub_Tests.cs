using System.IO;
using eBdb.EpubReader;
using NUnit.Framework;

namespace UnitTests {
	[TestFixture]
	public class Epub_Tests {
		private Epub _FitzgeraldBook;
		private Epub _LehovBook;
		private Epub _LoLeSartanBooks;

		[TestFixtureSetUp]
		public void SetupBooks() {
			_FitzgeraldBook = new Epub(@"c:\Inetpub\ePubReader\UnitTests\UnitTestsBooks\fitzgerald-curious-case-of-benjamin-button.epub");
			_LehovBook = new Epub(@"c:\Inetpub\ePubReader\UnitTests\UnitTestsBooks\LeholLhitpalelLehov_nadav.epub");
			_LoLeSartanBooks = new Epub(@"c:\Inetpub\ePubReader\UnitTests\UnitTestsBooks\LoLasartan.epub");
		}

		[Test]
		public void Test_UUID() {
			Assert.AreEqual("urn:uuid:CBC56AFC-6C29-1014-8672-92A1DF1F0AF1", _FitzgeraldBook.UUID);
			Assert.AreEqual("urn:uuid:9c8b24d6-a653-f975-e70c-4ad8962f888e", _LehovBook.UUID);
			Assert.AreEqual("urn:uuid:eb653363-ebc6-236c-c20b-ba3fc1cfa513", _LoLeSartanBooks.UUID);
		}

		[Test]
		public void Test_ID() {
			Assert.AreEqual(1, _FitzgeraldBook.ID.Count);
			Assert.AreEqual("urn:uuid:CBC56AFC-6C29-1014-8672-92A1DF1F0AF1", _FitzgeraldBook.ID[0]);

			Assert.AreEqual(1, _LehovBook.ID.Count);
			Assert.AreEqual("urn:uuid:9c8b24d6-a653-f975-e70c-4ad8962f888e", _LehovBook.ID[0]);

			Assert.AreEqual(1, _LoLeSartanBooks.ID.Count);
			Assert.AreEqual("urn:uuid:eb653363-ebc6-236c-c20b-ba3fc1cfa513", _LoLeSartanBooks.ID[0]);
		}

		[Test]
		public void Test_Title() {
			Assert.AreEqual(1, _FitzgeraldBook.Title.Count);
			Assert.AreEqual("The Curious Case of Benjamin Button", _FitzgeraldBook.Title[0]);

			Assert.AreEqual(1, _LehovBook.Title.Count);
			Assert.AreEqual("לאכול להתפלל לאהוב", _LehovBook.Title[0]);

			Assert.AreEqual(0, _LoLeSartanBooks.Title.Count);
		}

		[Test]
		public void Test_Language() {
			Assert.AreEqual(1, _FitzgeraldBook.Language.Count);
			Assert.AreEqual("en-gb", _FitzgeraldBook.Language[0]);

			Assert.AreEqual(1, _LehovBook.Language.Count);
			Assert.AreEqual("he", _LehovBook.Language[0]);

			Assert.AreEqual(1, _LoLeSartanBooks.Language.Count);
			Assert.AreEqual("en", _LoLeSartanBooks.Language[0]);
		}

		[Test]
		public void Test_Creator() {
			Assert.AreEqual(1, _FitzgeraldBook.Creator.Count);
			Assert.AreEqual("F. Scott Fitzgerald", _FitzgeraldBook.Creator[0]);

			Assert.AreEqual(1, _LehovBook.Creator.Count);
			Assert.AreEqual("אליזבת גילברט", _LehovBook.Creator[0]);

			Assert.AreEqual(0, _LoLeSartanBooks.Creator.Count);
		}

		[Test]
		public void Test_Description() {
			Assert.AreEqual(0, _FitzgeraldBook.Description.Count);

			Assert.AreEqual(0, _LehovBook.Description.Count);

			Assert.AreEqual(0, _LoLeSartanBooks.Description.Count);
		}

		[Test]
		public void Test_Date() {
			Assert.AreEqual(2, _FitzgeraldBook.Date.Count);
			Assert.AreEqual("original-publication", _FitzgeraldBook.Date[0].Type);
			Assert.AreEqual("1922", _FitzgeraldBook.Date[0].Date);
			Assert.AreEqual("epub-publication", _FitzgeraldBook.Date[1].Type);
			Assert.AreEqual("2011-06-15", _FitzgeraldBook.Date[1].Date);

			Assert.AreEqual(0, _LehovBook.Date.Count);

			Assert.AreEqual(0, _LoLeSartanBooks.Date.Count);
		}

		[Test]
		public void Test_Publisher() {
			Assert.AreEqual(1, _FitzgeraldBook.Publisher.Count);
			Assert.AreEqual("epubBooks (www.epubbooks.com)", _FitzgeraldBook.Publisher[0]);

			Assert.AreEqual(0, _LehovBook.Publisher.Count);

			Assert.AreEqual(0, _LoLeSartanBooks.Publisher.Count);
		}

		[Test]
		public void Test_Contributer() {
			Assert.AreEqual(0, _FitzgeraldBook.Contributer.Count);

			Assert.AreEqual(0, _LehovBook.Contributer.Count);

			Assert.AreEqual(0, _LoLeSartanBooks.Contributer.Count);
		}

		[Test]
		public void Test_Type() {
			Assert.AreEqual(0, _FitzgeraldBook.Type.Count);

			Assert.AreEqual(0, _LehovBook.Type.Count);

			Assert.AreEqual(0, _LoLeSartanBooks.Type.Count);
		}

		[Test]
		public void Test_Format() {
			Assert.AreEqual(0, _FitzgeraldBook.Format.Count);

			Assert.AreEqual(0, _LehovBook.Format.Count);

			Assert.AreEqual(0, _LoLeSartanBooks.Format.Count);
		}

		[Test]
		public void Test_Subject() {
			Assert.AreEqual(1, _FitzgeraldBook.Subject.Count);
			Assert.AreEqual("Short Stories", _FitzgeraldBook.Subject[0]);

			Assert.AreEqual(0, _LehovBook.Subject.Count);

			Assert.AreEqual(0, _LoLeSartanBooks.Subject.Count);
		}

		[Test]
		public void Test_Source() {
			Assert.AreEqual(1, _FitzgeraldBook.Source.Count);
			Assert.AreEqual("Project Gutenberg", _FitzgeraldBook.Source[0]);

			Assert.AreEqual(0, _LehovBook.Source.Count);

			Assert.AreEqual(0, _LoLeSartanBooks.Source.Count);
		}
		
		[Test]
		public void Test_Relation() {
			Assert.AreEqual(0, _FitzgeraldBook.Relation.Count);

			Assert.AreEqual(0, _LehovBook.Relation.Count);

			Assert.AreEqual(0, _LoLeSartanBooks.Relation.Count);
		}

		[Test]
		public void Test_Coverage() {
			Assert.AreEqual(0, _FitzgeraldBook.Coverage.Count);

			Assert.AreEqual(0, _LehovBook.Coverage.Count);

			Assert.AreEqual(0, _LoLeSartanBooks.Coverage.Count);
		}

		[Test]
		public void Test_Rights() {
			Assert.AreEqual(1, _FitzgeraldBook.Rights.Count);
			Assert.IsTrue(_FitzgeraldBook.Rights[0].Trim().StartsWith("Provided for free by epubBooks.com. Not for commercial use."));

			Assert.AreEqual(1, _LehovBook.Rights.Count);
			Assert.AreEqual("כנרת זמורה-ביתן", _LehovBook.Rights[0]);

			Assert.AreEqual(0, _LoLeSartanBooks.Rights.Count);
		}

		[Test]
		public void Test_Content() {
			Assert.AreEqual(13, _FitzgeraldBook.Content.Count);

			Assert.AreEqual("title.html", ((ContentData)_FitzgeraldBook.Content[0]).FileName);
			Assert.AreEqual("epubbooksinfo.html", ((ContentData)_FitzgeraldBook.Content[1]).FileName);
			Assert.AreEqual("chapter-001.html", ((ContentData)_FitzgeraldBook.Content[2]).FileName);
			Assert.AreEqual("chapter-002.html", ((ContentData)_FitzgeraldBook.Content[3]).FileName);
			Assert.AreEqual("chapter-003.html", ((ContentData)_FitzgeraldBook.Content[4]).FileName);
			Assert.AreEqual("chapter-004.html", ((ContentData)_FitzgeraldBook.Content[5]).FileName);
			Assert.AreEqual("chapter-005.html", ((ContentData)_FitzgeraldBook.Content[6]).FileName);
			Assert.AreEqual("chapter-006.html", ((ContentData)_FitzgeraldBook.Content[7]).FileName);
			Assert.AreEqual("chapter-007.html", ((ContentData)_FitzgeraldBook.Content[8]).FileName);
			Assert.AreEqual("chapter-008.html", ((ContentData)_FitzgeraldBook.Content[9]).FileName);
			Assert.AreEqual("chapter-009.html", ((ContentData)_FitzgeraldBook.Content[10]).FileName);
			Assert.AreEqual("chapter-010.html", ((ContentData)_FitzgeraldBook.Content[11]).FileName);
			Assert.AreEqual("chapter-011.html", ((ContentData)_FitzgeraldBook.Content[12]).FileName);

			Assert.AreEqual(File.ReadAllText(@"c:\Inetpub\ePubReader\UnitTests\UnitTestsBooks\fritz\OPS\title.html"), ((ContentData)_FitzgeraldBook.Content[0]).Content);
			Assert.AreEqual(File.ReadAllText(@"c:\Inetpub\ePubReader\UnitTests\UnitTestsBooks\fritz\OPS\epubbooksinfo.html"), ((ContentData)_FitzgeraldBook.Content[1]).Content);
			Assert.AreEqual(File.ReadAllText(@"c:\Inetpub\ePubReader\UnitTests\UnitTestsBooks\fritz\OPS\chapter-001.html"), ((ContentData)_FitzgeraldBook.Content[2]).Content);
			Assert.AreEqual(File.ReadAllText(@"c:\Inetpub\ePubReader\UnitTests\UnitTestsBooks\fritz\OPS\chapter-002.html"), ((ContentData)_FitzgeraldBook.Content[3]).Content);
			Assert.AreEqual(File.ReadAllText(@"c:\Inetpub\ePubReader\UnitTests\UnitTestsBooks\fritz\OPS\chapter-003.html"), ((ContentData)_FitzgeraldBook.Content[4]).Content);
			Assert.AreEqual(File.ReadAllText(@"c:\Inetpub\ePubReader\UnitTests\UnitTestsBooks\fritz\OPS\chapter-004.html"), ((ContentData)_FitzgeraldBook.Content[5]).Content);
			Assert.AreEqual(File.ReadAllText(@"c:\Inetpub\ePubReader\UnitTests\UnitTestsBooks\fritz\OPS\chapter-005.html"), ((ContentData)_FitzgeraldBook.Content[6]).Content);
			Assert.AreEqual(File.ReadAllText(@"c:\Inetpub\ePubReader\UnitTests\UnitTestsBooks\fritz\OPS\chapter-006.html"), ((ContentData)_FitzgeraldBook.Content[7]).Content);
			Assert.AreEqual(File.ReadAllText(@"c:\Inetpub\ePubReader\UnitTests\UnitTestsBooks\fritz\OPS\chapter-007.html"), ((ContentData)_FitzgeraldBook.Content[8]).Content);
			Assert.AreEqual(File.ReadAllText(@"c:\Inetpub\ePubReader\UnitTests\UnitTestsBooks\fritz\OPS\chapter-008.html"), ((ContentData)_FitzgeraldBook.Content[9]).Content);
			Assert.AreEqual(File.ReadAllText(@"c:\Inetpub\ePubReader\UnitTests\UnitTestsBooks\fritz\OPS\chapter-009.html"), ((ContentData)_FitzgeraldBook.Content[10]).Content);
			Assert.AreEqual(File.ReadAllText(@"c:\Inetpub\ePubReader\UnitTests\UnitTestsBooks\fritz\OPS\chapter-010.html"), ((ContentData)_FitzgeraldBook.Content[11]).Content);
			Assert.AreEqual(File.ReadAllText(@"c:\Inetpub\ePubReader\UnitTests\UnitTestsBooks\fritz\OPS\chapter-011.html"), ((ContentData)_FitzgeraldBook.Content[12]).Content);

			Assert.AreEqual(117, _LehovBook.Content.Count);
			Assert.AreEqual("LeholLhitpalelLehov-3.xhtml", ((ContentData)_LehovBook.Content[0]).FileName);
			Assert.AreEqual("LeholLhitpalelLehov-4.xhtml", ((ContentData)_LehovBook.Content[1]).FileName);
			Assert.AreEqual("LeholLhitpalelLehov-5.xhtml", ((ContentData)_LehovBook.Content[2]).FileName);
			Assert.AreEqual("LeholLhitpalelLehov-117.xhtml", ((ContentData)_LehovBook.Content[114]).FileName);
			Assert.AreEqual("LeholLhitpalelLehov-118.xhtml", ((ContentData)_LehovBook.Content[115]).FileName);
			Assert.AreEqual("LeholLhitpalelLehov-119.xhtml", ((ContentData)_LehovBook.Content[116]).FileName);

			Assert.AreEqual(File.ReadAllText(@"c:\Inetpub\ePubReader\UnitTests\UnitTestsBooks\lehov\OEBPS\LeholLhitpalelLehov-3.xhtml"), ((ContentData)_LehovBook.Content[0]).Content);
			Assert.AreEqual(File.ReadAllText(@"c:\Inetpub\ePubReader\UnitTests\UnitTestsBooks\lehov\OEBPS\LeholLhitpalelLehov-4.xhtml"), ((ContentData)_LehovBook.Content[1]).Content);
			Assert.AreEqual(File.ReadAllText(@"c:\Inetpub\ePubReader\UnitTests\UnitTestsBooks\lehov\OEBPS\LeholLhitpalelLehov-5.xhtml"), ((ContentData)_LehovBook.Content[2]).Content);
			Assert.AreEqual(File.ReadAllText(@"c:\Inetpub\ePubReader\UnitTests\UnitTestsBooks\lehov\OEBPS\LeholLhitpalelLehov-117.xhtml"), ((ContentData)_LehovBook.Content[114]).Content);
			Assert.AreEqual(File.ReadAllText(@"c:\Inetpub\ePubReader\UnitTests\UnitTestsBooks\lehov\OEBPS\LeholLhitpalelLehov-118.xhtml"), ((ContentData)_LehovBook.Content[115]).Content);
			Assert.AreEqual(File.ReadAllText(@"c:\Inetpub\ePubReader\UnitTests\UnitTestsBooks\lehov\OEBPS\LeholLhitpalelLehov-119.xhtml"), ((ContentData)_LehovBook.Content[116]).Content);

			Assert.AreEqual(1, _LoLeSartanBooks.Content.Count);
			Assert.AreEqual("LoLasartan1.xhtml", ((ContentData)_LoLeSartanBooks.Content[0]).FileName);
			Assert.AreEqual(File.ReadAllText(@"c:\Inetpub\ePubReader\UnitTests\UnitTestsBooks\lolesartan\OEBPS\LoLasartan1.xhtml"), ((ContentData)_LoLeSartanBooks.Content[0]).Content);
		}

		[Test]
		public void Test_ExtendedData() {
			Assert.AreEqual(4, _FitzgeraldBook.ExtendedData.Count);
			Assert.AreEqual("css/titlepage.css", ((ExtendedData)_FitzgeraldBook.ExtendedData[0]).FileName);
			Assert.AreEqual("text/css", ((ExtendedData)_FitzgeraldBook.ExtendedData[0]).MimeType);
			Assert.IsTrue(((ExtendedData)_FitzgeraldBook.ExtendedData[0]).IsText);
			Assert.AreEqual(File.ReadAllText(@"c:\Inetpub\ePubReader\UnitTests\UnitTestsBooks\fritz\OPS\css\titlepage.css"), ((ExtendedData)_FitzgeraldBook.ExtendedData[0]).Content);

			Assert.AreEqual("images/epubbooks-logo.png", ((ExtendedData)_FitzgeraldBook.ExtendedData[2]).FileName);
			Assert.AreEqual("image/png", ((ExtendedData)_FitzgeraldBook.ExtendedData[2]).MimeType);
			Assert.IsFalse(((ExtendedData)_FitzgeraldBook.ExtendedData[2]).IsText);

			Assert.AreEqual(23, _LehovBook.ExtendedData.Count);
			Assert.AreEqual("images/460878-5_fmt.jpeg", ((ExtendedData)_LehovBook.ExtendedData[1]).FileName);
			Assert.AreEqual("image/jpeg", ((ExtendedData)_LehovBook.ExtendedData[1]).MimeType);
			Assert.IsFalse(((ExtendedData)_LehovBook.ExtendedData[1]).IsText);

			Assert.AreEqual(25, _LoLeSartanBooks.ExtendedData.Count);
			Assert.AreEqual("images/figure 19_fmt.jpeg", ((ExtendedData)_LoLeSartanBooks.ExtendedData[23]).FileName);
			Assert.AreEqual("image/jpeg", ((ExtendedData)_LoLeSartanBooks.ExtendedData[23]).MimeType);
			Assert.IsFalse(((ExtendedData)_LoLeSartanBooks.ExtendedData[23]).IsText);

			Assert.AreEqual("toc.ncx", ((ExtendedData)_LoLeSartanBooks.ExtendedData[0]).FileName);
			Assert.AreEqual("application/x-dtbncx+xml", ((ExtendedData)_LoLeSartanBooks.ExtendedData[0]).MimeType);
			Assert.IsTrue(((ExtendedData)_LoLeSartanBooks.ExtendedData[0]).IsText);
			Assert.AreEqual(File.ReadAllText(@"c:\Inetpub\ePubReader\UnitTests\UnitTestsBooks\lolesartan\OEBPS\toc.ncx"), ((ExtendedData)_LoLeSartanBooks.ExtendedData[0]).Content);
		}

		[Test]
		public void Test_TOC() {
			Assert.AreEqual(13, _FitzgeraldBook.TOC.Count);
			Assert.AreEqual(0, _FitzgeraldBook.TOC[0].Children.Count);
			Assert.AreEqual(File.ReadAllText(@"c:\Inetpub\ePubReader\UnitTests\UnitTestsBooks\fritz\OPS\title.html"), _FitzgeraldBook.TOC[0].ContentData.Content);
			Assert.AreEqual("navpoint-1", _FitzgeraldBook.TOC[0].ID);
			Assert.AreEqual(1, _FitzgeraldBook.TOC[0].Order);
			Assert.AreEqual("title.html", _FitzgeraldBook.TOC[0].Source);
			Assert.AreEqual("Title Page", _FitzgeraldBook.TOC[0].Title);

			Assert.AreEqual("navpoint-2", _FitzgeraldBook.TOC[1].ID);
			Assert.AreEqual("navpoint-3", _FitzgeraldBook.TOC[2].ID);
			Assert.AreEqual("navpoint-4", _FitzgeraldBook.TOC[3].ID);
			Assert.AreEqual("navpoint-5", _FitzgeraldBook.TOC[4].ID);
			Assert.AreEqual("navpoint-6", _FitzgeraldBook.TOC[5].ID);
			Assert.AreEqual("navpoint-7", _FitzgeraldBook.TOC[6].ID);
			Assert.AreEqual("navpoint-8", _FitzgeraldBook.TOC[7].ID);
			Assert.AreEqual("navpoint-9", _FitzgeraldBook.TOC[8].ID);
			Assert.AreEqual("navpoint-10", _FitzgeraldBook.TOC[9].ID);
			Assert.AreEqual("navpoint-11", _FitzgeraldBook.TOC[10].ID);
			Assert.AreEqual("navpoint-12", _FitzgeraldBook.TOC[11].ID);
			Assert.AreEqual("navpoint-13", _FitzgeraldBook.TOC[12].ID);

			Assert.AreEqual(11, _LehovBook.TOC.Count);
			Assert.AreEqual(35, _LehovBook.TOC[6].Children.Count);
			Assert.AreEqual(File.ReadAllText(@"c:\Inetpub\ePubReader\UnitTests\UnitTestsBooks\lehov\OEBPS\LeholLhitpalelLehov-9.xhtml"), _LehovBook.TOC[6].ContentData.Content);
			Assert.AreEqual("navpoint-7", _LehovBook.TOC[6].ID);
			Assert.AreEqual(8, _LehovBook.TOC[6].Order);
			Assert.AreEqual("LeholLhitpalelLehov-9.xhtml", _LehovBook.TOC[6].Source);
			Assert.AreEqual("ספר ראשון - איטליה", _LehovBook.TOC[6].Title);

			Assert.AreEqual(File.ReadAllText(@"c:\Inetpub\ePubReader\UnitTests\UnitTestsBooks\lehov\OEBPS\LeholLhitpalelLehov-10.xhtml"), _LehovBook.TOC[6].Children[0].ContentData.Content);
			Assert.AreEqual("navpoint-8", _LehovBook.TOC[6].Children[0].ID);
			Assert.AreEqual(9, _LehovBook.TOC[6].Children[0].Order);
			Assert.AreEqual("LeholLhitpalelLehov-10.xhtml", _LehovBook.TOC[6].Children[0].Source);
			Assert.AreEqual("1", _LehovBook.TOC[6].Children[0].Title);

			Assert.AreEqual(1, _LoLeSartanBooks.TOC.Count);
			Assert.AreEqual(16, _LoLeSartanBooks.TOC[0].Children.Count);
			Assert.AreEqual(File.ReadAllText(@"c:\Inetpub\ePubReader\UnitTests\UnitTestsBooks\lolesartan\OEBPS\LoLasartan1.xhtml"), _LoLeSartanBooks.TOC[0].Children[0].ContentData.Content);
			Assert.AreEqual("navpoint-1", _LoLeSartanBooks.TOC[0].Children[0].ID);
			Assert.AreEqual(2, _LoLeSartanBooks.TOC[0].Children[0].Order);
			Assert.AreEqual("LoLasartan1.xhtml#toc-anchor", _LoLeSartanBooks.TOC[0].Children[0].Source);
			Assert.AreEqual("מבוא", _LoLeSartanBooks.TOC[0].Children[0].Title);

			Assert.AreEqual(File.ReadAllText(@"c:\Inetpub\ePubReader\UnitTests\UnitTestsBooks\lolesartan\OEBPS\LoLasartan1.xhtml"), _LoLeSartanBooks.TOC[0].Children[15].ContentData.Content);
			Assert.AreEqual("navpoint-16", _LoLeSartanBooks.TOC[0].Children[15].ID);
			Assert.AreEqual(17, _LoLeSartanBooks.TOC[0].Children[15].Order);
			Assert.AreEqual("LoLasartan1.xhtml#toc-anchor-15", _LoLeSartanBooks.TOC[0].Children[15].Source);
			Assert.AreEqual("חלק ראשון - הרפואה התזונתית החדשה", _LoLeSartanBooks.TOC[0].Children[15].Title);
		}
	}
}
