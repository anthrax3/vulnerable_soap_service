using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.XPath;

namespace fuzzer
{
	public class WSDL
	{
		public WSDL (XmlDocument doc)
		{
			ParseTypes (doc);
			ParseMessages (doc);
			ParsePortTypes (doc);
			ParseBindings (doc);
			ParseServices (doc);
		}

		public List<SoapType> Types { get; set; }

		public List<SoapMessage> Messages { get; set; }

		public List<SoapPortType> PortTypes { get; set; }

		public List<SoapBinding> Bindings { get; set; }

		public List<SoapService> Services { get; set; }

		private void ParseTypes (XmlDocument wsdl)
		{
			this.Types = new List<SoapType> ();
			foreach (XmlNode node in wsdl.LastChild.ChildNodes) {
				if (node.Name.EndsWith ("types")) {
					foreach (XmlNode type in node.FirstChild.ChildNodes)
						this.Types.Add (new SoapType (type));
				}
			}
		}

		private void ParseMessages (XmlDocument wsdl)
		{
			this.Messages = new List<SoapMessage> ();
			foreach (XmlNode node in wsdl.LastChild.ChildNodes) {
				if (node.Name.EndsWith ("message"))
					this.Messages.Add (new SoapMessage (node));
			}
		}

		private void ParsePortTypes (XmlDocument wsdl)
		{
			this.PortTypes = new List<SoapPortType> ();
			foreach (XmlNode node in wsdl.LastChild.ChildNodes) {
				if (node.Name.EndsWith ("portType"))
					this.PortTypes.Add (new SoapPortType (node));
			}
		}

		private void ParseBindings (XmlDocument wsdl)
		{
			this.Bindings = new List<SoapBinding> ();
			foreach (XmlNode node in wsdl.LastChild.ChildNodes) {
				if (node.Name.EndsWith ("binding"))
					this.Bindings.Add (new SoapBinding (node));
			}
		}

		private void ParseServices (XmlDocument wsdl)
		{
			this.Services = new List<SoapService> ();
			foreach (XmlNode node in wsdl.LastChild.ChildNodes) {
				if (node.Name.EndsWith ("service"))
					this.Services.Add (new SoapService (node));
			}
		}
	}
}

