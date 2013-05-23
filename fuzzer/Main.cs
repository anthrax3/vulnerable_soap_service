using System;
using System.Linq;
using System.Xml;
using System.Net;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace fuzzer
{
	class MainClass
	{
		private static WSDL _wsdl = null;
		private static string _endpoint = null;

		public static void Main (string[] args)
		{
			_endpoint = args [0];

			Console.WriteLine ("Fetching the WSDL for service: " + _endpoint);

			HttpWebRequest req = (HttpWebRequest)WebRequest.Create (_endpoint + "?WSDL");
			XmlDocument wsdlDoc = new XmlDocument ();

			using (StreamReader rdr = new StreamReader(req.GetResponse().GetResponseStream()))
				wsdlDoc.LoadXml (rdr.ReadToEnd ());

			_wsdl = new WSDL (wsdlDoc);

			Console.WriteLine ("Fetched and loaded the web service description.");

			foreach (SoapService service in _wsdl.Services) {
				FuzzService (service);
			}
		}

		static void FuzzService (SoapService service)
		{
			Console.WriteLine ("Fuzzing service: " + service.Name);

			foreach (SoapPort port in service.Ports) { 
				Console.WriteLine ("Fuzzing " + port.ElementType.Split (':') [0] + " port: " + port.Name);
				SoapBinding binding = _wsdl.Bindings.Where (b => b.Name == port.Binding.Split (':') [1]).Single ();

				if (binding.IsHTTP)
					FuzzHttpPort(binding);
				else
					FuzzSoapPort(binding);
			}
		}

		static void FuzzHttpPort (SoapBinding binding)
		{
			if (binding.Verb == "GET")
				FuzzHttpGetPort(binding);
			else if (binding.Verb == "POST")
				FuzzHttpPostPort(binding);
			else
				throw new Exception("Don't know verb: " + binding.Verb);
		}

		static void FuzzSoapPort (SoapBinding binding)
		{
			SoapPortType portType = _wsdl.PortTypes.Where (pt => pt.Name == binding.Type.Split (':') [1]).Single ();

			foreach (SoapBindingOperation op in binding.Operations) {
				Console.WriteLine ("Fuzzing operation: " + op.Name);

				string url = _endpoint;
				SoapOperation po = portType.Operations.Where (p => p.Name == op.Name).Single ();
				SoapMessage input = _wsdl.Messages.Where (m => m.Name == po.Input.Split (':') [1]).Single ();

				string soap = "<?xml version=\"1.0\" encoding=\"utf-16\"?>";
				soap += "<soap:Envelope xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"";
				soap += " xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\"";
				soap += " xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\">";
				soap += "<soap:Body>";
				soap += "<" + op.Name + " xmlns=\"" + op.SoapAction.Replace (op.Name, string.Empty) + "\">";

				int i = 0;
				SoapType type = null; //this is cheating
				foreach (SoapPart part in input.Parts) {
					type = _wsdl.Types.Where (t => t.Name == part.Element.Split (':') [1]).Single ();

					foreach (SoapTypeParameter param in type.Parameters) {
						soap += "<" + param.Name + ">";

						if (param.Type.EndsWith ("string"))
							soap += "fds" + i++;

						soap += "</" + param.Name + ">";
					}
				}

				soap += "</" + op.Name + ">";
				soap += "</soap:Body>";
				soap += "</soap:Envelope>";

				for (int k = 0; k <= i; k++) {
					string testSoap = soap.Replace ("fds" + k, "fd'sa");
					byte[] data = System.Text.Encoding.ASCII.GetBytes (testSoap);

					HttpWebRequest req = (HttpWebRequest)WebRequest.Create (url);
					req.Headers ["SOAPAction"] = op.SoapAction;
					req.Method = "POST";
					req.ContentType = "text/xml";
					req.ContentLength = data.Length;
					req.GetRequestStream ().Write (data, 0, data.Length);

					string resp = string.Empty;
					try {
						using (StreamReader rdr = new StreamReader(req.GetResponse().GetResponseStream()))
							resp = rdr.ReadToEnd ();
					} catch (WebException ex) {
						using (StreamReader rdr = new StreamReader(ex.Response.GetResponseStream()))
							resp = rdr.ReadToEnd ();

						if (resp.Contains ("syntax error"))
							Console.WriteLine ("Possible SQL injection vector in parameter: " + type.Parameters[k].Name);

					}
				}
			}
		}		

		static void FuzzHttpGetPort (SoapBinding binding)
		{
			SoapPortType portType = _wsdl.PortTypes.Where (pt => pt.Name == binding.Type.Split (':') [1]).Single ();

			foreach (SoapBindingOperation op in binding.Operations) {
				Console.WriteLine ("Fuzzing operation: " + op.Name);

				string url = _endpoint + op.Location;
				SoapOperation po = portType.Operations.Where (p => p.Name == op.Name).Single ();
				SoapMessage input = _wsdl.Messages.Where (m => m.Name == po.Input.Split (':') [1]).Single ();

				Dictionary<string, string> parameters = new Dictionary<string, string> ();
				foreach (SoapPart part in input.Parts) {
					parameters.Add(part.Name, part.Type);
				}

				bool first = true;
				int i = 0;
				foreach (var param in parameters) {
					if (param.Value.EndsWith ("string"))
						url +=  (first ? "?" : "&") + param.Key + "=fds" + i++;
					if (first)
						first = false;
				}

				Console.WriteLine ("Fuzzing full url: " + url);

				for (int k = 0; k <= i; k++) {
					string testUrl = url.Replace("fds" + k, "fd'sa");
					HttpWebRequest req = (HttpWebRequest)WebRequest.Create(testUrl);
					string resp = string.Empty;
					try {
						using (StreamReader rdr = new StreamReader(req.GetResponse().GetResponseStream()))
							resp = rdr.ReadToEnd();
					}
					catch(WebException ex) {
						using (StreamReader rdr = new StreamReader(ex.Response.GetResponseStream()))
							resp = rdr.ReadToEnd();

						if (resp.Contains("syntax error"))
							Console.WriteLine("Possible SQL injection vector in parameter: " + input.Parts[k].Name);
					}
				}
			}
		}		

		static void FuzzHttpPostPort (SoapBinding binding)
		{
			SoapPortType portType = _wsdl.PortTypes.Where (pt => pt.Name == binding.Type.Split (':') [1]).Single ();
			foreach (SoapBindingOperation op in binding.Operations) {
				Console.WriteLine ("Fuzzing operation: " + op.Name);

				string url = _endpoint + op.Location;
				SoapOperation po = portType.Operations.Where (p => p.Name == op.Name).Single ();
				SoapMessage input = _wsdl.Messages.Where (m => m.Name == po.Input.Split (':') [1]).Single ();
				Dictionary<string, string> parameters = new Dictionary<string, string> ();

				foreach (SoapPart part in input.Parts) {
					parameters.Add(part.Name, part.Type);
				}

				string postParams = string.Empty;
				bool first = true;
				int i = 0;
				foreach (var param in parameters) {
					if (param.Value.EndsWith ("string"))
						postParams +=  (first ? "" : "&") + param.Key + "=fds" + i++;
					if (first)
						first = false;
				}

				Console.WriteLine ("Fuzzing full url: " + url);

				for (int k = 0; k <= i; k++) {
					string testParams = postParams.Replace("fds" + k, "fd'sa");
					byte[] data = System.Text.Encoding.ASCII.GetBytes(testParams);

					HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
					req.Method = "POST";
					req.ContentType = "application/x-www-form-urlencoded";
					req.ContentLength = data.Length;
					req.GetRequestStream().Write(data, 0, data.Length);

					string resp = string.Empty;
					try {
						using (StreamReader rdr = new StreamReader(req.GetResponse().GetResponseStream()))
							resp = rdr.ReadToEnd();
					}
					catch(WebException ex) {
						using (StreamReader rdr = new StreamReader(ex.Response.GetResponseStream()))
							resp = rdr.ReadToEnd();

						if (resp.Contains("syntax error"))
							Console.WriteLine("Possible SQL injection vector in parameter: " + input.Parts[k].Name);
					}
				}
			}
		}
	}
}
	 