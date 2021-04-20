using NiceIO;
using System;
using System.IO;
using System.Xml;

namespace Unity.Platforms.Android.Build
{
    class AndroidXmlDocument : XmlDocument
    {
        protected XmlNamespaceManager nsMgr;
        public const string AndroidXmlNamespace = "http://schemas.android.com/apk/res/android";

        public AndroidXmlDocument(NPath path)
        {
            using (var reader = new XmlTextReader(new StringReader(path.ReadAllText())))
            {
                reader.Read();
                Load(reader);
            }
            nsMgr = new XmlNamespaceManager(NameTable);
            nsMgr.AddNamespace("android", AndroidXmlNamespace);
        }


        public string GetContents()
        {
            using (var stringWriter = new StringWriter())
            using (var xmlTextWriter = new XmlTextWriter(stringWriter))
            {
                xmlTextWriter.Formatting = Formatting.Indented;
                WriteTo(xmlTextWriter);
                xmlTextWriter.Flush();
                return stringWriter.GetStringBuilder().ToString();
            }
        }
        /*
        public string SaveAs(string path)
        {
            using (var writer = new XmlTextWriter(path, new UTF8Encoding(false)))
            {
                writer.Formatting = Formatting.Indented;
                Save(writer);
            }
            return path;
        }*/

        public XmlAttribute CreateAttribute(string prefix, string localName, string namezpace, string value)
        {
            XmlAttribute attr = CreateAttribute(prefix, localName, namezpace);
            attr.Value = value;
            return attr;
        }

        protected XmlElement AppendElement(XmlNode node, string tag, string attribute)
        {
            if (node.SelectSingleNode(String.Format(".//{0}[@{1}]", tag, attribute), nsMgr) != null)
                return null;
            return node.AppendChild(CreateElement(tag)) as XmlElement;
        }

        protected XmlElement AppendElement(XmlNode node, string tag, string attribute, string attributeValue)
        {
            if (node.SelectSingleNode(String.Format(".//{0}[@{1}='{2}']", tag, attribute, attributeValue), nsMgr) != null)
                return null;
            return node.AppendChild(CreateElement(tag)) as XmlElement;
        }

        public void PatchStringRes(string tag, string attrib, string value)
        {
            XmlNode node = SelectSingleNode(String.Format("//{0}[@name='{1}']", tag, attrib), nsMgr);
            if (node == null)
            {
                node = DocumentElement.AppendChild(CreateElement(tag));
                node.Attributes.Append(CreateAttribute("", "name", "", attrib));
            }
            // http://developer.android.com/guide/topics/resources/string-resource.html#FormattingAndStyling
            value = value.Replace(@"\", @"\\").Replace(@"'", @"\'").Replace("\"", "\\\"");
            node.InnerText = value;
        }
    }
}
