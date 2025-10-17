using System;
using System.Collections.Generic;
using System.Linq;

namespace GameApp.Service.Extensions
{
    public static class EnumerableExtensions
    {
        public static T? GetRandom<T>(this IEnumerable<T> source, Random? rng = null)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));
            rng ??= Random.Shared;

            if (source is IList<T> list)
            {
                return list.Count == 0 ? default : list[rng.Next(list.Count)];
            }

            var buffer = source.ToList();
            return buffer.Count == 0 ? default : buffer[rng.Next(buffer.Count)];
        }
    }
} 