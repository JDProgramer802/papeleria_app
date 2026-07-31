using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Papeleria.Business.Common;

/// <summary>Lee el texto <see cref="DisplayAttribute"/> de los enumerados del dominio.</summary>
public static class Enumeraciones
{
    private static readonly Dictionary<Enum, string> Cache = new();

    /// <summary>Nombre legible del valor; si no tiene atributo, devuelve el nombre del miembro.</summary>
    public static string Descripcion(this Enum valor)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(valor, out var texto))
            {
                return texto;
            }

            var miembro = valor.GetType().GetField(valor.ToString());
            var atributo = miembro?.GetCustomAttribute<DisplayAttribute>();

            texto = atributo?.Name ?? valor.ToString();
            Cache[valor] = texto;

            return texto;
        }
    }

    /// <summary>Lista de valores de un enumerado junto con su descripción, para combos.</summary>
    public static IReadOnlyList<OpcionEnum<T>> Opciones<T>() where T : struct, Enum =>
        Enum.GetValues<T>().Select(v => new OpcionEnum<T>(v, ((Enum)(object)v).Descripcion())).ToList();
}

/// <summary>Par valor/descripción usado para poblar los desplegables de la interfaz.</summary>
public record OpcionEnum<T>(T Valor, string Descripcion) where T : struct, Enum
{
    public override string ToString() => Descripcion;
}
