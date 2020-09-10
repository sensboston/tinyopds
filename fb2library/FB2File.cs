using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using FB2Library.Elements;
using FB2Library.HeaderItems;

namespace FB2Library
{
    public class FB2File
    {
        private XNamespace fileNameSpace = XNamespace.None;

        private const string TitleInfoElementName = "title-info";
        private const string SrcTitleInfoElementName = "src-title-info";
        private const string DocumentInfoElementName = "document-info";
        private const string Fb2TextDescriptionElementName = "description";
        private const string Fb2TextBodyElementName = "body";
        private const string Fb2BinaryElementName = "binary";


        private readonly ItemTitleInfo titleInfo = new ItemTitleInfo();
        private readonly ItemTitleInfo srcTitleInfo = new ItemTitleInfo();
        private readonly ItemDocumentInfo documentInfo = new ItemDocumentInfo();
        private readonly ItemPublishInfo publishInfo = new ItemPublishInfo();
        private readonly List<ItemCustomInfo> customInfo = new List<ItemCustomInfo>();
        private BodyItem mainBody = null;
        private readonly List<BodyItem> bodiesList = new List<BodyItem>();
        private readonly Dictionary<string, BinaryItem> binaryObjects = new Dictionary<string, BinaryItem>();
        private readonly List<StyleElement> styles = new List<StyleElement>();
        private readonly List<ShareInstructionType> output = new List<ShareInstructionType>();



        /// <summary>
        /// Get list of images (the image data itself) stored as binary objects
        /// </summary>
        public Dictionary<string, BinaryItem> Images { get { return binaryObjects;} }


        /// <summary>
        /// Get list of bodies in document (including main)
        /// </summary>
        public List<BodyItem> Bodies { get { return bodiesList;}}

        /// <summary>
        /// Get Document TitleInfo structure
        /// </summary>
        public ItemTitleInfo TitleInfo { get { return titleInfo; } }

        /// <summary>
        /// Get Document DocumentInfo structure
        /// </summary>
        public ItemDocumentInfo DocumentInfo { get { return documentInfo; } }

        /// <summary>
        /// Get Document SourceTitleInfo structure
        /// </summary>
        public ItemTitleInfo SourceTitleInfo { get { return srcTitleInfo; } }


        /// <summary>
        /// Get custom added information (if available)
        /// </summary>
        public List<ItemCustomInfo> CustomInfo { get { return customInfo; } }

        /// <summary>
        /// Get Document PublishInfo structure
        /// </summary>
        public ItemPublishInfo PublishInfo { get { return publishInfo; } }

        /// <summary>
        /// Get main body structure (book itself)
        /// </summary>
        public BodyItem MainBody { get { return mainBody; } }

        /// <summary>
        /// Get styles applied to the book 
        /// </summary>
        public List<StyleElement> StyleSheets { get { return styles; } }


        /// <summary>
        /// Loads the file as data from XML data
        /// </summary>
        /// <param name="fileDocument">XML document containing the file</param>
        /// <param name="bLoadHeaderOnly">if true loads only header information</param>
        public void Load(XDocument fileDocument,bool bLoadHeaderOnly)
        {
            if (fileDocument == null)
            {
                throw new ArgumentNullException("fileDocument");
            }
            if (fileDocument.Root == null)
            {
                throw new ArgumentException("Document's root is NULL (empty document passed)");
            }


            // theoretically the namespace should be "http://www.gribuser.ru/xml/fictionbook/2.0" but just to be sure with invalid files
            fileNameSpace = fileDocument.Root.GetDefaultNamespace();


            styles.Clear();
            IEnumerable<XElement> xStyles = fileDocument.Elements(fileNameSpace + StyleElement.StyleElementName);
            // attempt to load some bad FB2 with wrong namespace
            if (xStyles.Count() == 0)
            {
                xStyles = fileDocument.Elements(StyleElement.StyleElementName);
            }
            foreach (var style in xStyles)
            {
                StyleElement element = new StyleElement();
                try
                {
                    element.Load(style);
                    styles.Add(element);
                }
                catch (Exception)
                {
                    continue;
                }
            }

            LoadDescriptionSection(fileDocument, fileNameSpace, !bLoadHeaderOnly);

            if (!bLoadHeaderOnly)
            {
                XNamespace namespaceUsed = fileNameSpace;
                // Load body elements (first is main text)
                IEnumerable<XElement> xBodys = fileDocument.Root.Elements(fileNameSpace + Fb2TextBodyElementName);
                // try to read some badly formated FB2 files
                if (xBodys.Count() == 0)
                {
                    namespaceUsed = "";
                    xBodys = fileDocument.Root.Elements(namespaceUsed + Fb2TextBodyElementName);
                }
                foreach (var body in xBodys)
                {
                    BodyItem bodyItem = new BodyItem() { NameSpace = namespaceUsed };
                    try
                    {
                        bodyItem.Load(body);
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                    bodiesList.Add(bodyItem);
                }
                if (bodiesList.Count > 0)
                {
                    mainBody = bodiesList[0];   
                }


                // Load binaries sections (currently images only)
                IEnumerable<XElement> xBinaryes = fileDocument.Root.Elements(namespaceUsed + Fb2BinaryElementName);
                if (xBinaryes.Count()==0)
                {
                    xBinaryes = fileDocument.Root.Elements(Fb2BinaryElementName);
                }
                foreach (var binarye in xBinaryes)
                {
                    BinaryItem item = new BinaryItem();
                    try
                    {
                        item.Load(binarye);
                    }
                    catch (Exception)
                    {
                        
                        continue;
                    }
                    // add just unique IDs to fix some invalid FB2s 
                    if (!binaryObjects.ContainsKey(item.Id))
                    {
                        binaryObjects.Add(item.Id, item);                        
                    }
                }
            }

        }

        private void LoadDescriptionSection(XDocument fileDocument, XNamespace xNamespace, bool loadBinaryItems = true)
        {
            if (fileDocument == null)
            {
                throw new ArgumentNullException("fileDocument");
            }
            if (fileDocument.Root == null)
            {
                throw new NullReferenceException("LoadDescriptionSection: Root is null");
            }
            XElement xTextDescription = fileDocument.Root.Element(fileNameSpace + Fb2TextDescriptionElementName);
            // attempt to load some bad FB2 with wrong namespace
            XNamespace namespaceUsed = fileNameSpace;
            if (xTextDescription == null)
            {
                namespaceUsed = "";
                xTextDescription = fileDocument.Root.Element(Fb2TextDescriptionElementName);
            }
            if (xTextDescription != null)
            {
                // Load Title info 
                XElement xTitleInfo = xTextDescription.Element(namespaceUsed + TitleInfoElementName);
                if (xTitleInfo != null)
                {
                    titleInfo.Namespace = namespaceUsed;
                    try
                    {
                        titleInfo.Load(xTitleInfo);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(string.Format("Error reading title info : {0}", ex.Message));
                    }

                }

                // Load Src Title info 
                XElement xSrcTitleInfo = xTextDescription.Element(namespaceUsed + SrcTitleInfoElementName);
                if (xSrcTitleInfo != null)
                {
                    srcTitleInfo.Namespace = fileNameSpace;
                    try
                    {
                        srcTitleInfo.Load(xSrcTitleInfo);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(string.Format("Error reading source title info : {0}", ex.Message));
                    }
                }

                // Load document info
                XElement xDocumentInfo = xTextDescription.Element(namespaceUsed + DocumentInfoElementName);
                if (xDocumentInfo != null)
                {
                    documentInfo.Namespace = fileNameSpace;
                    try
                    {
                        documentInfo.Load(xDocumentInfo);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(string.Format("Error reading document info : {0}", ex.Message));
                    }
                }

                // Load publish info 
                XElement xPublishInfo = xTextDescription.Element(namespaceUsed + ItemPublishInfo.PublishInfoElementName);
                if (xPublishInfo != null)
                {
                    publishInfo.Namespace = fileNameSpace;
                    try
                    {
                        publishInfo.Load(xPublishInfo);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(string.Format("Error reading publishing info : {0}", ex.Message));
                        //Debug.WriteLine(string.Format("Error reading publishing info : {0}", ex.Message));
                    }
                }

                XElement xCustomInfo = xTextDescription.Element(namespaceUsed + ItemCustomInfo.CustomInfoElementName);
                if (xCustomInfo != null)
                {
                    ItemCustomInfo CustElement = new ItemCustomInfo();
                    CustElement.Namespace = fileNameSpace;
                    try
                    {
                        CustElement.Load(xCustomInfo);
                        customInfo.Add(CustElement);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(string.Format("Error reading custom info : {0}", ex.Message));
                        //Debug.WriteLine(string.Format("Error reading custom info : {0}", ex.Message));
                    }
                }

                IEnumerable<XElement> xInstructions = xTextDescription.Elements(xTextDescription.Name.Namespace + "output");
                int outputCount = 0;
                output.Clear();
                foreach (var xInstruction in xInstructions)
                {
                    // only two elements allowed by scheme
                    if (outputCount > 1)
                    {
                        break;
                    }
                    ShareInstructionType outp = new ShareInstructionType { Namespace = namespaceUsed };
                    try
                    {
                        outp.Load(xInstruction);
                        output.Add(outp);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(string.Format("Error reading output instructions : {0}", ex.Message));
                        continue;
                    }
                    finally
                    {
                        outputCount++;
                    }
                }

                if (loadBinaryItems && titleInfo.Cover != null)
                {

                    foreach (InlineImageItem coverImag in titleInfo.Cover.CoverpageImages)
                    {
                        if (string.IsNullOrEmpty(coverImag.HRef))
                        {
                            continue;
                        }
                        string coverref;
                        if (coverImag.HRef.Substring(0, 1) == "#")
                        {
                            coverref = coverImag.HRef.Substring(1);
                        }
                        else
                        {
                            coverref = coverImag.HRef;
                        }
                        IEnumerable<XElement> xBinaryes =
                            fileDocument.Root.Elements(fileNameSpace + Fb2BinaryElementName).Where(
                                cov => ((cov.Attribute("id") != null) && (cov.Attribute("id").Value == coverref)));
                        foreach (var binarye in xBinaryes)
                        {
                            BinaryItem item = new BinaryItem();
                            try
                            {
                                item.Load(binarye);
                            }
                            catch (Exception)
                            {

                                continue;
                            }
                            // add just unique IDs to fix some invalid FB2s 
                            if (!binaryObjects.ContainsKey(item.Id))
                            {
                                binaryObjects.Add(item.Id, item);
                            }
                        }
                    }
                }
            }
        }

        public XDocument ToXML(bool bExportHeaderOnly)
        {
            XDocument fileDocument = new XDocument(new XDeclaration("1.0","UTF-8","no"));

            XElement xFictionBook = new XElement(Fb2Const.fb2DefaultNamespace+"FictionBook",
                        new XAttribute(XNamespace.Xmlns + "l", @"http://www.w3.org/1999/xlink"),
                        new XAttribute("xmlns", Fb2Const.fb2DefaultNamespace));

            fileDocument.Add(xFictionBook);

            foreach (StyleElement style in styles)
            {
                try
                {
                    xFictionBook.Add(style.ToXML());
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(string.Format("Error converting style data to XML: {0}", ex.Message));
                    continue;
                }
            }

            XElement xDescription = new XElement(Fb2Const.fb2DefaultNamespace+ Fb2TextDescriptionElementName);
            xFictionBook.Add(xDescription);

            try
            {
                xDescription.Add(titleInfo.ToXML(TitleInfoElementName));
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("Error converting TitleInfo data to XML: {0}", ex.Message));
            }

            if (srcTitleInfo.BookTitle!=null)
            {
                try
                {
                    xDescription.Add(srcTitleInfo.ToXML(SrcTitleInfoElementName));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(string.Format("Error converting SrcTitleInfo data to XML: {0}", ex.Message));
                }
            }

            try
            {
                xDescription.Add(documentInfo.ToXML());
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("Error converting DocumentInfo data to XML: {0}", ex.Message));
            }

            xDescription.Add(publishInfo.ToXML());
            foreach (ItemCustomInfo Custinfo in customInfo)
            {
                xDescription.Add(Custinfo.ToXML());
            }
            foreach (ShareInstructionType outp in output)
            {
                xDescription.Add(outp.ToXML());
            }



            if (!bExportHeaderOnly)
            {
                foreach (BodyItem bodyItem in bodiesList)
                {
                    xFictionBook.Add(bodyItem.ToXML());
                }
                foreach (KeyValuePair<string, BinaryItem> BItem in binaryObjects)
                {
                    xFictionBook.Add(BItem.Value.ToXML());
                }
            }
            else
            {
                foreach (InlineImageItem coverImag in titleInfo.Cover.CoverpageImages)
                {
                    if (string.IsNullOrEmpty(coverImag.HRef))
                    {
                        continue;
                    }
                    string coverref;
                    if (coverImag.HRef.Substring(0, 1) == "#")
                    {
                        coverref = coverImag.HRef.Substring(1);
                    }
                    else
                    {
                        coverref = coverImag.HRef;
                    }
                    xFictionBook.Add(binaryObjects[coverref].ToXML());
                }
            }

            return fileDocument;
        }

    }
}
