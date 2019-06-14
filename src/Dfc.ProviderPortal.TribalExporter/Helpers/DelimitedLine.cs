using Dfc.ProviderPortal.Packages;
using Dfc.ProviderPortal.TribalExporter.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dfc.ProviderPortal.TribalExporter.Helpers
{
    public class DelimitedLine : ValueObject<DelimitedLine>, IDelimitedLine
    {
        public int Number { get; }
        public IReadOnlyList<IDelimitedField> Fields { get; }

        public DelimitedLine(
            int number,
            IEnumerable<IDelimitedField> fields)
        {
            if (number < 1) throw new ArgumentException("Must be greater than zero.", nameof(number));
            Number = number;
            Fields = fields.ToList().AsReadOnly();
        }

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Number;
            yield return Fields;
        }
    }
}
