using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using FB2Library.Elements;

// TODO: make genre list instead of just string 
// TODO: add coverpage format and reading
// TODO: add sequence format and reading


namespace FB2Library.HeaderItems
{
    /// <summary>
    /// Book (as a book opposite a document) description
    /// </summary>
    public class ItemTitleInfo
    {
        private const string GenreElementName = "genre";
        private const string BookTitleElementName = "book-title";
        private const string AnnotationElementName = "annotation";
        private const string KeywordsElementName = "keywords";
        private const string CoverPageElementName = "coverpage";
        private const string LanguageElementName = "lang";
        private const string SourceLanguageElementName = "src-lang";

        private List<TitleGenreType> genres = new List<TitleGenreType>();

        private List<AuthorType> translators = new List<AuthorType>();

        private List<AuthorType> bookAuthors = new List<AuthorType>();

        private List<SequenceType> sequences = new List<SequenceType>();

        private XNamespace fileNameSpace = XNamespace.None;



        /// <summary>
        /// Get list of sequences
        /// </summary>
        public List<SequenceType> Sequences { get { return sequences; } }

        /// <summary>
        /// XML namespace used to read the document
        /// </summary>
        public XNamespace Namespace
        {
            set { fileNameSpace = value; }
            get { return fileNameSpace; }
        }

        /// <summary>
        /// Translators if this is a translation
        /// </summary>
        public IEnumerable<AuthorType> Translators
        {
            get { return this.translators; }
        }

        /// <summary>
        /// Book's source language if this is a translation
        /// </summary>
        public string SrcLanguage { get; set; }

        /// <summary>
        /// Keywords used by search engine
        /// </summary>
        public TextFieldType Keywords { get; set; }

        /// <summary>
        /// Genres of the item
        /// </summary>
        public IEnumerable<TitleGenreType> Genres
        {
            get { return genres; }
        }


        /// <summary>
        /// Authors of this book
        /// </summary>
        public IEnumerable<AuthorType> BookAuthors
        {
            get { return bookAuthors; }
        }

        /// <summary>
        /// Book Title
        /// </summary>
        public TextFieldType BookTitle { set; get; }

        /// <summary>
        /// Book's annotation
        /// </summary>
        public AnnotationItem Annotation { set; get; }

        /// <summary>
        /// Book date , can be a range like 1990-1991
        /// </summary>
        public DateItem BookDate { get; set; }

        /// <summary>
        /// Book's language
        /// </summary>
        public string Language { set; get; }

        public CoverPage Cover { get; set; }

        public void Load(XElement xTitleInfo)
        {
            if ( xTitleInfo == null )
            {
                throw new ArgumentNullException("xTitleInfo");
            }

            // Load genres
            genres.Clear();
            IEnumerable<XElement> xGenres = xTitleInfo.Elements(fileNameSpace + GenreElementName);
            if ( xGenres != null )
            {
                foreach ( XElement xGenre in xGenres )
                {
                    if ( (xGenre != null) && (xGenre.Value != null) )
                    {
                        TitleGenreType genre = new TitleGenreType();
                        genre.Genre = xGenre.Value;
                        XAttribute xMatch = xGenre.Attribute("match");
                        if (xMatch != null && !string.IsNullOrEmpty(xMatch.Value))
                        {
                            int percentage;
                            if (int.TryParse(xMatch.Value,out percentage))
                            {
                                genre.Match = percentage;
                            }
                        }
                        genres.Add(genre);
                    }
                }
            }

            // Load authors
            bookAuthors.Clear();
            IEnumerable<XElement> xAuthors = xTitleInfo.Elements(fileNameSpace + AuthorType.AuthorElementName);
            if ( xAuthors != null )
            {
                foreach (XElement xAuthor in xAuthors )
                {
                    AuthorItem author = new AuthorItem { Namespace = fileNameSpace };
                    try
                    {
                        author.Load(xAuthor);
                        bookAuthors.Add(author);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(string.Format("Error reading author: {0}",ex.Message));
                        continue;
                    }
                }
            }
            

            // Load Title
            BookTitle = null;
            XElement xBookTitle = xTitleInfo.Element(fileNameSpace + BookTitleElementName);
            if (xBookTitle != null) 
            {
                BookTitle = new TextFieldType();
                try
                {
                    BookTitle.Load(xBookTitle);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(string.Format("Error reading book title: {0}", ex.Message));
                }
            }

            // Load Annotation
            Annotation = null;
            XElement xAnnotation = xTitleInfo.Element(fileNameSpace + AnnotationElementName);
            if (xAnnotation != null) 
            {
                Annotation = new AnnotationItem();
                try
                {
                    Annotation.Load(xAnnotation);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(string.Format("Error reading annotation: {0}", ex.Message));
                }
            }


            // Load keywords
            Keywords = null;
            XElement xKeywords = xTitleInfo.Element(fileNameSpace + KeywordsElementName);
            if (xKeywords != null) 
            {
                Keywords    =   new TextFieldType();
                try
                {
                    Keywords.Load(xKeywords);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(string.Format("Error reading keywords: {0}", ex.Message));
                }
            }

            // Load Book date
            BookDate = null;
            XElement xBookDate = xTitleInfo.Element(fileNameSpace + DateItem.Fb2DateElementName);
            if (xBookDate != null) 
            {
                BookDate = new DateItem();
                try
                {
                    BookDate.Load(xBookDate);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(string.Format("Error reading book date: {0}", ex.Message));
                }
            }

            Cover = null;
            // we should load coverpage images here but no use for them as for now
            XElement xCoverPage = xTitleInfo.Element(fileNameSpace + CoverPageElementName);
            if ( xCoverPage != null)
            {
                Cover = new CoverPage{Namespace = fileNameSpace};
                try
                {
                    Cover.Load(xCoverPage);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(string.Format("Error reading cover: {0}", ex.Message));
                }
            }

            // Load Language
            Language = null;
            XElement xLanguage = xTitleInfo.Element(fileNameSpace + LanguageElementName);
            if ( (xLanguage != null) && ( xLanguage.Value != null ))
            {
                Language = xLanguage.Value;
            }
            else
            {
                Debug.WriteLine("Language not specified in title section");                
            }

            // Load source language
            SrcLanguage = null;
            XElement xSrcLanguage = xTitleInfo.Element(fileNameSpace + SourceLanguageElementName);
            if ( (xSrcLanguage != null) && (xSrcLanguage.Value != null) )
            {
                SrcLanguage = xSrcLanguage.Value;
            }

            // Load translators 
            translators.Clear();
            IEnumerable<XElement> xTranslators = xTitleInfo.Elements(fileNameSpace + AuthorType.TranslatorElementName);
            if ( xTranslators != null )
            {
                foreach ( XElement xTranslator in xTranslators )
                {
                    TranslatorItem translator = new TranslatorItem() { Namespace = fileNameSpace };
                    try
                    {
                        translator.Load(xTranslator);
                        translators.Add(translator);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(string.Format("Error reading translator: {0}", ex.Message));
                        continue;
                    }
                }
            }

            // Load sequences
            sequences.Clear();
            IEnumerable<XElement> xSequences = xTitleInfo.Elements(fileNameSpace + SequenceType.SequenceElementName);
            foreach (var xSequence in xSequences)
            {
               SequenceType sec = new SequenceType(){ Namespace = fileNameSpace };
                try
                {
                    sec.Load(xSequence);
                    if (!string.IsNullOrEmpty(sec.Name)) sequences.Add(sec);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(string.Format("Error reading sequence data: {0}", ex.Message));
                    //Debug.WriteLine(string.Format("Error reading sequence data: {0}",ex.Message));
                    continue;
                }
            }
        }

        public XElement ToXML(string NameElement)
        {
            XElement xTitleInfo = new XElement(Fb2Const.fb2DefaultNamespace + NameElement);
            foreach (TitleGenreType genre in genres)
            {
                try
                {
                    xTitleInfo.Add(genre.ToXML());
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(string.Format("Error converting genre data to XML: {0}", ex.Message));
                    continue;                    
                }
            }
            foreach (AuthorType author in bookAuthors)
            {
                try
                {
                    xTitleInfo.Add(author.ToXML());
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(string.Format("Error converting genre data to XML: {0}", ex.Message));
                    continue;
                }
            }

            if (BookTitle != null)
            {
                xTitleInfo.Add(BookTitle.ToXML(BookTitleElementName));
            }

            if (Annotation != null)
            {
                xTitleInfo.Add(Annotation.ToXML());
            }
            if (Keywords != null)
            {
                xTitleInfo.Add(Keywords.ToXML(KeywordsElementName));
            }
            if (BookDate != null)
            {
                xTitleInfo.Add(BookDate.ToXML());
            }
            if (Cover != null)
            {
                xTitleInfo.Add(Cover.ToXML());
            }
            if (!string.IsNullOrEmpty(Language))
            {
                xTitleInfo.Add(new XElement(Fb2Const.fb2DefaultNamespace + LanguageElementName, Language));
            }
            if (!string.IsNullOrEmpty(SrcLanguage))
            {
                xTitleInfo.Add(new XElement(Fb2Const.fb2DefaultNamespace + SourceLanguageElementName, SrcLanguage));
            }
            foreach (AuthorType translat in translators)
            {
                xTitleInfo.Add(translat.ToXML());
            }
            foreach (SequenceType sec in sequences)
            {
                xTitleInfo.Add(sec.ToXML());
            }

            return xTitleInfo;
        }

    } // class
}
