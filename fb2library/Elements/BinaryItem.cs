using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace FB2Library.Elements
{
    public enum ContentTypeEnum
    {
       ContentTypeJpeg,
       ContentTypePng,
       ContentTypeGif,
    }

    public class BinaryItem
    {
        private const string ContentTypeAttributeName = "content-type";
        private const string IdAttributeName = "id";

        public ContentTypeEnum ContentType{get;set;}
        public Byte[] BinaryData { get; set; }
        public string Id { get; set; }



        internal const string Fb2BinaryItemName = "binary";

        internal void Load(XElement binarye)
        {
            if (binarye == null)
            {
                throw new ArgumentNullException("binarye");
            }

            if (binarye.Name.LocalName != Fb2BinaryItemName)
            {
                throw new ArgumentException("Element of wrong type passed", "binarye");
            }

            XAttribute xContentType = binarye.Attribute(ContentTypeAttributeName);
            if ((xContentType == null) || (xContentType.Value == null))
            {
                throw new NullReferenceException("content type not defined/present");
            }
            switch (xContentType.Value.ToLower())
            {
                case "image/jpeg":
                case "image/jpg":
                    ContentType = ContentTypeEnum.ContentTypeJpeg;
                    break;
                case "image/png":
                    ContentType = ContentTypeEnum.ContentTypePng;
                    break;
                case "image/gif":
                    ContentType = ContentTypeEnum.ContentTypeGif;
                    break;
                default:
                    throw new Exception("Unknown image content type passed");

            }

            XAttribute idAttribute = binarye.Attribute(IdAttributeName);
            if ((idAttribute == null) || (idAttribute.Value == null))
            {
                throw new NullReferenceException("ID not defined/present");
            }
            Id = idAttribute.Value;

            if (BinaryData != null)
            {
                BinaryData= null;
            }
            BinaryData = Convert.FromBase64String(binarye.Value);
            ContentTypeEnum content = ContentType;
            DetectContentType(ref content, BinaryData);
            ContentType = content;
        }

        private void DetectContentType(ref ContentTypeEnum contentType, byte[] BinaryData)
        {
            try
            {

                using (MemoryStream imgStream = new MemoryStream(BinaryData))
                {
                    using (Bitmap bitmap = new Bitmap(imgStream))
                    {
                        if (bitmap.RawFormat.Equals(ImageFormat.Jpeg))
                        {
                            contentType = ContentTypeEnum.ContentTypeJpeg;
                        }
                        else if (bitmap.RawFormat.Equals(ImageFormat.Png))
                        {
                            contentType = ContentTypeEnum.ContentTypePng;
                        }
                        else if (bitmap.RawFormat.Equals(ImageFormat.Gif))
                        {
                            contentType = ContentTypeEnum.ContentTypeGif;
                        }
                    }
                }
            }
            catch (Exception ex)
            {

                throw new Exception(string.Format("Error during image type detection: {0}",ex.Message));
            }

        }

        protected string GetXContentType()
        {
            switch (ContentType)
            {
                case ContentTypeEnum.ContentTypeJpeg:
                    return "image/jpg";
                case ContentTypeEnum.ContentTypePng:
                    return "image/png";
                case ContentTypeEnum.ContentTypeGif:
                    return "image/gif";
                default:
                    return "";

            }
        }

        public XElement ToXML()
        {
            XElement xBinary = new XElement(Fb2Const.fb2DefaultNamespace + Fb2BinaryItemName);
            xBinary.Add(new XAttribute(ContentTypeAttributeName,GetXContentType()));
            xBinary.Add(new XAttribute(IdAttributeName,Id));
            xBinary.Value=Convert.ToBase64String(BinaryData);

            return xBinary;

        }
    }
}
