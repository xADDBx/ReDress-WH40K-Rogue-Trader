using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReDress;
public enum Outfit {
    [Description("Unchanged")]
    Current,
    [Description("Criminal")]
    Criminal,
    [Description("Nobility")]
    Nobility,
    [Description("Commissar")]
    Commissar,
    [Description("Navy Officer")]
    Navy,
    [Description("Astra Militarum")]
    Militarum,
    [Description("Sanctioned Psyker")]
    Psyker,
    [Description("Ministorum Crusader")]
    Crusader,
    [Description("Navigator")]
    Navigator,
    [Description("Arbitrator")]
    Arbitrator,
    [Description("None of the other (Naked for non-companions)")]
    Naked
}
