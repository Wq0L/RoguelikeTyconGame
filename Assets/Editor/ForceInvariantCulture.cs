using System.Globalization;
using System.Threading;
using UnityEditor;

[InitializeOnLoad]
public class ForceInvariantCulture
{
    static ForceInvariantCulture()
    {
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
    }
}
