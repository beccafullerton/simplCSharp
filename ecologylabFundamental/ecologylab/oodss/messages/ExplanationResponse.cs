//
//  ExplanationResponse.cs
//  s.im.pl serialization
//
//  Generated by DotNetTranslator on 04/09/11.
//  Copyright 2011 Interface Ecology Lab. 
//

using System;
using System.Collections.Generic;
using ecologylab.attributes;

namespace ecologylab.oodss.messages 
{
	/// <summary>
	/// missing java doc comments or could not find the source file.
	/// </summary>
	[simpl_inherit]
	public class ExplanationResponse<S> : ResponseMessage<S>
	{
		/// <summary>
		/// missing java doc comments or could not find the source file.
		/// </summary>
		[simpl_scalar]
		private String explanation;

		public ExplanationResponse()
		{ }

		public String Explanation
		{
			get{return explanation;}
			set{explanation = value;}
		}
	}
}
