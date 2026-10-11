using System.Linq.Expressions;
using System.Reflection;
using Majlis.Framework.Domain.Exceptions;

namespace Majlis.Framework.EntityFrameworkCore.Repositories;

/// <summary>Applies a sorting string such as <c>"Name Asc, CreationTime Desc"</c>. Only public properties are accepted.</summary>
public static class SortingExtensions
{
    public static IQueryable<T> ApplySorting<T>(this IQueryable<T> query, string sorting)
    {
        var first = true;
        foreach (var part in sorting.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var tokens = part.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var property = typeof(T).GetProperty(tokens[0], BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                ?? throw new CustomValidationException("General:Fields:InvalidSorting", tokens[0]);
            var descending = tokens.Length > 1 && tokens[1].Equals("desc", StringComparison.OrdinalIgnoreCase);

            var parameter = Expression.Parameter(typeof(T), "e");
            var keySelector = Expression.Lambda(Expression.Property(parameter, property), parameter);
            var method = (first, descending) switch
            {
                (true, false) => nameof(Queryable.OrderBy),
                (true, true) => nameof(Queryable.OrderByDescending),
                (false, false) => nameof(Queryable.ThenBy),
                (false, true) => nameof(Queryable.ThenByDescending),
            };

            query = (IQueryable<T>)query.Provider.CreateQuery(Expression.Call(
                typeof(Queryable), method, [typeof(T), property.PropertyType], query.Expression, Expression.Quote(keySelector)));
            first = false;
        }

        return query;
    }
}
