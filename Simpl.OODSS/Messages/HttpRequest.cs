//
//  HttpRequest.cs
//  s.im.pl serialization
//
//  Generated by DotNetTranslator on 04/09/11.
//  Copyright 2011 Interface Ecology Lab. 
//

using Simpl.Fundamental.Net;
using Simpl.Serialization.Attributes;
using ecologylab.collections;

namespace Simpl.OODSS.Messages 
{
	/// <summary>
	/// missing java doc comments or could not find the source file.
	/// </summary>
	[SimplInherit]
    public class HttpRequest : RequestMessage
	{
		/// <summary>
		/// missing java doc comments or could not find the source file.
		/// </summary>
		[SimplScalar]
		private ParsedUri _okResponseUrl;

		/// <summary>
		/// missing java doc comments or could not find the source file.
		/// </summary>
		[SimplScalar]
		private ParsedUri _errorResponseUrl;

		public HttpRequest()
		{ }

		public ParsedUri OkResponseUrl
		{
			get{return _okResponseUrl;}
			set{_okResponseUrl = value;}
		}

		public ParsedUri ErrorResponseUrl
		{
			get{return _errorResponseUrl;}
			set{_errorResponseUrl = value;}
		}

	    public override ResponseMessage PerformService(Scope<object> clientSessionScope)
	    {
	        throw new System.NotImplementedException();
	    }
	}
}
