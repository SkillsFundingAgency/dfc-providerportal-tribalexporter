using Dfc.ProviderPortal.Packages;
using Dfc.ProviderPortal.TribalExporter.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace Dfc.ProviderPortal.TribalExporter.Helpers
{
    public class DelimitedField : ValueObject<DelimitedField>, IDelimitedField
    {
        public int Number { get; }
        public string Value { get; }
        public bool IsDoubleQuoted { get; }

        public DelimitedField(int number, string value, bool isDoubleQuoted = false)
        {
            if (number < 1) throw new ArgumentException("Must be greater than zero.", nameof(number));
            Number = number;
            Value = value;
            IsDoubleQuoted = isDoubleQuoted;
        }

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Number;
            yield return Value;
            yield return IsDoubleQuoted;
        }
    }
}
