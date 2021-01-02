using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using static RVis.Base.Check;

namespace RVis.Data.Extensions
{
  public static class TableExt
  {
    public static IDataColumn ToDataColumn(this Array data, string name)
    {
      var elementType = data.GetType().GetElementType();
      RequireNotNull(elementType);
      var typedColumn = typeof(DataColumn<>).MakeGenericType(elementType);
      var instance = Activator.CreateInstance(typedColumn, new object[] { name, data });
      RequireNotNull(instance);
      return (IDataColumn)instance;
    }

    public static IDataColumn<T> ToDataColumn<T>(this T[] data, string name) => new DataColumn<T>(name, data);

    public static IDataColumn<T> ToDataColumn<T>(this IEnumerable<T> data, string name) => new DataColumn<T>(name, data);

    public static NumDataColumn ToDataColumn(this double[] data, string name) => new NumDataColumn(name, data);

    public static NumDataColumn ToDataColumn(this IEnumerable<double> data, string name) => new NumDataColumn(name, data);

    public static IDataColumn<T> Get<T>(this IDataTable dataTable, string name) => EnsureDataType<T>(dataTable[name]);

    public static IDataColumn<T> Get<T>(this IDataTable dataTable, int index) => EnsureDataType<T>(dataTable[index]);

    private static IDataColumn<T> EnsureDataType<T>([NotNull] IDataColumn? dataColumn) =>
      dataColumn is IDataColumn<T> dt ?
        dt :
        throw new ArgumentException($"Requested column of type {typeof(T).Name}; runtime type is {dataColumn?.GetDataType().Name}", nameof(dataColumn));
  }
}
