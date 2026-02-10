using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MangaMesh.Shared.Models
{
    public enum ReleaseType
    {
        VerifiedScanlation,   // Signed / known group
        UnverifiedScanlation, // Claims a scanlator name but unsigned
        RoughTranslation,     // Early / speed / MTL / solo translator
        Raw,                  // No translation
        Unknown
    }
}