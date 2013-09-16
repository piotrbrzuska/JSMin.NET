﻿/* This is a .NET port of the Douglas Crockford's JSMin 'C' project.
 * The author's copyright message is reproduced below.
 */

/* jsmin.c
   2013-03-29

Copyright (c) 2002 Douglas Crockford  (www.crockford.com)

Permission is hereby granted, free of charge, to any person obtaining a copy of
this software and associated documentation files (the "Software"), to deal in
the Software without restriction, including without limitation the rights to
use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies
of the Software, and to permit persons to whom the Software is furnished to do
so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

The Software shall be used for Good, not Evil.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using System;
using System.IO;

namespace DouglasCrockford.JsMin
{
    public sealed class JsMinifier
    {
        const int EOF = -1;

		StringReader sr;
		StringWriter sw;
        int theA;
        int theB;
        int theLookahead = EOF;
		int theX = EOF;
		int theY = EOF;

		/* isAlphanum -- return true if the character is a letter, digit, underscore,
				dollar sign, or non-ASCII character.
		*/
		static bool isAlphanum(int c)
		{
			return ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') ||
					(c >= 'A' && c <= 'Z') || c == '_' || c == '$' || c == '\\' ||
					c > 126);
		}

		/* get -- return the next character from stdin. Watch out for lookahead. If
				the character is a control character, translate it to a space or
				linefeed.
		*/
		int get()
		{
			int c = theLookahead;
			theLookahead = EOF;
			if (c == EOF)
			{
				c = sr.Read();
			}
			if (c >= ' ' || c == '\n' || c == EOF)
			{
				return c;
			}
			if (c == '\r')
			{
				return '\n';
			}
			return ' ';
		}

		/* peek -- get the next character without getting it.
		*/
		int peek()
		{
			theLookahead = get();
			return theLookahead;
		}

		/* next -- get the next character, excluding comments. peek() is used to see
				if a '/' is followed by a '/' or '*'.
		*/
		int next()
		{
			int c = get();
			if (c == '/')
			{
				switch (peek())
				{
					case '/':
						for (;;)
						{
							c = get();
							if (c <= '\n')
							{
								break;
							}
						}
						break;
					case '*':
						get();
						while (c != ' ')
						{
							switch (get())
							{
								case '*':
									if (peek() == '/')
									{
										get();
										c = ' ';
									}
									break;
								case EOF:
									throw new JsMinificationException("Unterminated comment.");
							}
						}
						break;
				}
			}
			theY = theX;
			theX = c;
			return c;
		}

		/* action -- do something! What you do is determined by the argument:
				1   Output A. Copy B to A. Get the next B.
				2   Copy B to A. Get the next B. (Delete A).
				3   Get the next B. (Delete B).
		   action treats a string as a single character. Wow!
		   action recognizes a regular expression if it is preceded by ( or , or =.
		*/
		void action(int d)
		{
			if (d == 1)
			{
				put(theA);
				if (
					(theY == '\n' || theY == ' ') &&
					(theA == '+' || theA == '-' || theA == '*' || theA == '/') &&
					(theB == '+' || theB == '-' || theB == '*' || theB == '/')
				)
				{
					put(theY);
				}
			}
			if (d <= 2)
			{
				theA = theB;
				if (theA == '\'' || theA == '"' || theA == '`')
				{
					for (;;)
					{
						put(theA);
						theA = get();
						if (theA == theB)
						{
							break;
						}
						if (theA == '\\')
						{
							put(theA);
							theA = get();
						}
						if (theA == EOF)
						{
							throw new JsMinificationException("Unterminated string literal.");
						}
					}
				}
			}
			if (d <= 3)
			{
				theB = next();
				if (theB == '/' && (
					theA == '(' || theA == ',' || theA == '=' || theA == ':' ||
					theA == '[' || theA == '!' || theA == '&' || theA == '|' ||
					theA == '?' || theA == '+' || theA == '-' || theA == '~' ||
					theA == '*' || theA == '/' || theA == '{' || theA == '\n'
				))
				{
					put(theA);
					if (theA == '/' || theA == '*')
					{
						put(' ');
					}
					put(theB);
					for (;;)
					{
						theA = get();
						if (theA == '[')
						{
							for (;;)
							{
								put(theA);
								theA = get();
								if (theA == ']')
								{
									break;
								}
								if (theA == '\\')
								{
									put(theA);
									theA = get();
								}
								if (theA == EOF)
								{
									throw new JsMinificationException("Unterminated set in Regular Expression literal.");
								}
							}
						}
						else if (theA == '/')
						{
							switch (peek())
							{
								case '/':
								case '*':
									throw new JsMinificationException("Unterminated set in Regular Expression literal.");
							}
							break;
						}
						else if (theA == '\\')
						{
							put(theA);
							theA = get();
						}
						if (theA == EOF) {
							throw new JsMinificationException("Unterminated Regular Expression literal.");
						}
						put(theA);
					}
					theB = next();
				}
			}
		}

		/* jsmin -- Copy the input to the output, deleting the characters which are
				insignificant to JavaScript. Comments will be removed. Tabs will be
				replaced with spaces. Carriage returns will be replaced with linefeeds.
				Most spaces and linefeeds will be removed.
		*/
		void jsmin()
		{
			if (peek() == 0xEF)
			{
				get();
				get();
				get();
			}
			theA = '\n';
			action(3);
			while (theA != EOF)
			{
				switch (theA)
				{
					case ' ':
						action(isAlphanum(theB) ? 1 : 2);
						break;
					case '\n':
						switch (theB)
						{
							case '{':
							case '[':
							case '(':
							case '+':
							case '-':
							case '!':
							case '~':
								action(1);
								break;
							case ' ':
								action(3);
								break;
							default:
								action(isAlphanum(theB) ? 1 : 2);
								break;
						}
						break;
					default:
						switch (theB)
						{
							case ' ':
								action(isAlphanum(theA) ? 1 : 3);
								break;
							case '\n':
								switch (theA)
								{
									case '}':
									case ']':
									case ')':
									case '+':
									case '-':
									case '"':
									case '\'':
									case '`':
										action(1);
										break;
									default:
										action(isAlphanum(theA) ? 1 : 3);
										break;
								}
								break;
							default:
								action(1);
								break;
						}
						break;
				}
			}
		}

		public String Minify(string js)
		{
			using (sr = new StringReader(js))
			{
				using (sw = new StringWriter())
				{
					jsmin();
					sw.Flush();

					return sw.ToString().TrimStart();
				}
			}
		}

		#region Methods for substitution methods of the C language
		void put(int c)
        {
            sw.Write((char)c);
		}
		#endregion
	}
}