using System;
using System.Text.RegularExpressions;

namespace ReleaseControlPanel.API
{
	public class EnvironmentSettings
	{
		public string Name { get; set; }
		public Regex NameRegex
		{
			get
			{
				if (_nameRegex == null)
				{
					_nameRegex = new Regex(Name);
				}

				return _nameRegex;
			}
		}
		public Environments Type { get; set; }

		private Regex _nameRegex;
	}
}
