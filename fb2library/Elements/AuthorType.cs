using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using FB2Library.Elements;

namespace FB2Library.HeaderItems
{

    public class AuthorType 
    {
        public const string AuthorElementName = "author";
        public const string TranslatorElementName = "translator";
        public const string PublisherElementName = "publisher";


        private const string FirstNameElementName = "first-name";
        private const string MiddleNameElementName = "middle-name";
        private const string LastNameElementName = "last-name";
        private const string NickNameElementName = "nickname";
        private const string HomePageElementName = "home-page";
        private const string EMailElementName = "email";
        private const string IdElementName = "id";


        private XNamespace fileNameSpace = XNamespace.None;

        private TextFieldType uid = new TextFieldType {Text = string.Empty};

        protected string GetElementName()
        {
            return ElementName;
        }

        public string ElementName { get; set; }

        /// <summary>
        /// XML namespace used to read the document
        /// </summary>
        public XNamespace Namespace
        {
            set { fileNameSpace = value; }
            get { return fileNameSpace; }
        }

        /// <summary>
        /// Author's first name
        /// </summary>
        public TextFieldType FirstName { get; set; }

        /// <summary>
        /// Author's middle name
        /// </summary>
        public TextFieldType MiddleName { get; set; }

        /// <summary>
        /// Author's Last name
        /// </summary>
        public TextFieldType LastName { get; set; }

        /// <summary>
        /// Author's UID
        /// </summary>
        public TextFieldType UID
        {
            get
            {
                return uid;
            }
            set
            {
                if (!string.IsNullOrEmpty(value.Text))
                {
                    uid = value;
                    uid.Text = uid.Text.ToLower();
                }
                else
                {
                    uid = value;
                }
            }
        }

        /// <summary>
        /// Author's nickname
        /// </summary>
        public TextFieldType NickName { get; set; }

        /// <summary>
        /// Authors home page
        /// </summary>
        public TextFieldType HomePage { get; set; }

        /// <summary>
        /// Author's e-mail address
        /// </summary>
        public TextFieldType EMail { get; set; }

        /// <summary>
        /// Loads Author's data from XML "author" node
        /// </summary>
        /// <param name="xElement">"author" XML node</param>
        /// <returns>true if succeeded, false if failed</returns>
        internal void Load(XElement xElement)
        {
            if ( xElement == null )
            {
                throw new ArgumentNullException("xElement");
            }

            // Load first name
            FirstName = null;
            XElement xFirstName = xElement.Element(fileNameSpace + FirstNameElementName);
            if (xFirstName != null) 
            {
                FirstName = new TextFieldType();
                try
                {
                    FirstName.Load(xFirstName);
                }
                catch (Exception)
                {
                }
            }

            // load middle name
            MiddleName = null;
            XElement xMiddleName = xElement.Element(fileNameSpace + MiddleNameElementName);
            if (xMiddleName != null) 
            {
                MiddleName = new TextFieldType();
                try
                {
                    MiddleName.Load(xMiddleName);
                }
                catch(Exception)
                {
                    
                }
            }

            // Load last name
            LastName = null;
            XElement xLastName = xElement.Element(fileNameSpace + LastNameElementName);
            if (xLastName != null) 
            {
                try
                {
                    LastName = new TextFieldType();
                    LastName.Load(xLastName);
                }
                catch (Exception)
                {
                }
            }


            // Load Nickname
            NickName = null;
            XElement xNickName = xElement.Element(fileNameSpace + NickNameElementName);
            if (xNickName != null) 
            {
                try
                {
                    NickName = new TextFieldType();
                    NickName.Load(xNickName);
                }
                catch (Exception)
                {
                }
            }

            // Load Homepage
            HomePage = null;
            XElement xHomePage = xElement.Element(fileNameSpace + HomePageElementName);
            if (xHomePage != null) 
            {
                try
                {
                    HomePage = new TextFieldType();
                    HomePage.Load(xHomePage);
                }
                catch (Exception)
                {
                }
            }


            //Load e-mail
            EMail = null;
            XElement xEMail = xElement.Element(fileNameSpace + EMailElementName);
            if (xEMail != null) 
            {
                try
                {
                    EMail = new TextFieldType();
                    EMail.Load(xEMail);
                }
                catch (Exception)
                {
                }
            }

            // Load UID
            uid = null;
            XElement xUID = xElement.Element(fileNameSpace + IdElementName);
            if (xUID != null) 
            {
                uid = new TextFieldType();
                try
                {
                    uid.Load(xUID);
                    uid.Text = UID.Text.ToLower();
                }
                catch (Exception)
                {
                }
            }

        }

        public override string ToString()
        {
            string firstName = "";
            if ( FirstName != null )
            {
                firstName = FirstName.Text;
            }

            string midName = "";
            if ( MiddleName != null )
            {
                midName = MiddleName.Text;
            }

            string lastName = "";
            if ( LastName != null )
            {
                lastName = LastName.Text;
            }

            string nickName = "no nick";
            if ( NickName != null )
            {
                nickName = NickName.Text;
            }

            string uid = "unknown-uid";
            if ( UID != null )
            {
                uid = UID.Text;
            }

            return string.Format("{0} {1} {2} ({3}): {4}",lastName,firstName,midName,nickName,uid);
        }

        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return ToString().Equals(obj.ToString());
        }

        public XElement ToXML( )
        {
            XElement xPerson = new XElement(Fb2Const.fb2DefaultNamespace + ElementName);
            if (FirstName != null)
            {
               xPerson.Add(FirstName.ToXML(FirstNameElementName));
            }
            if (MiddleName != null)
            {
                xPerson.Add(MiddleName.ToXML(MiddleNameElementName));
            }
            if (LastName != null)
            {
                xPerson.Add(LastName.ToXML(LastNameElementName));
            }
            if (NickName != null)
            {
                xPerson.Add(NickName.ToXML(NickNameElementName));
            }
            if (HomePage != null)
            {
                xPerson.Add(HomePage.ToXML(HomePageElementName));
            }
            if (EMail != null)
            {
                xPerson.Add(EMail.ToXML(EMailElementName));
            }
            if (UID != null)
            {
                xPerson.Add(UID.ToXML(IdElementName));
            }
            return xPerson;
        }
    }//class
}
