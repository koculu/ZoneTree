using System.Text;

namespace Tenray.ZoneTree.Core;

public static class TypeExtensions
{
    public static string SimplifiedFullName(this Type type)
    {
        if (!type.IsGenericType)
            return type.FullName;
        var builder = new StringBuilder();
        var typeName = type.Namespace + "." + type.Name;
        int index = typeName.IndexOf('`');
        if (index > 0)
        {
            builder.Append(typeName.Substring(0, index));
        }
        else
        {
            builder.Append(typeName);
        }

        builder.Append("<");
        var genericArguments = type.GetGenericArguments();
        var len = genericArguments.Length;
        for (var i = 0; i < len; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }
            builder.Append(genericArguments[i].SimplifiedFullName());
        }
        builder.Append(">");
        return builder.ToString();
    }
}