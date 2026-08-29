using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Testing.Generator;

internal sealed record MethodTarget
{
    public required string MethodName { get; init; }
    public required string TypeName { get; init; }
    public required string Namespace { get; init; }
    public required string DatasetId { get; init; }
    public required EquatableArray<MethodParameterTarget> Parameters { get; init; }
    public required string ReturnTypeName { get; init; }
}

internal sealed record MethodParameterTarget : IEquatable<MethodParameterTarget>
{
    public required string Name { get; init; }
    public required string TypeName { get; init; }

    public bool Equals(MethodParameterTarget? other) =>
        other is not null && Name == other.Name && TypeName == other.TypeName;

    public override int GetHashCode()
    {
        var hash = 17;
        hash = hash * 31 + Name.GetHashCode();
        hash = hash * 31 + TypeName.GetHashCode();
        return hash;
    }
}

internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
    where T : IEquatable<T>
{
    private readonly T[] _items;

    public EquatableArray(T[] items) => _items = items ?? [];

    public int Count => _items.Length;

    public T this[int index] => _items[index];

    public bool Equals(EquatableArray<T> other) => _items.SequenceEqual(other._items);

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        var hash = 0;
        foreach (var item in _items)
            hash = (hash * 397) ^ item.GetHashCode();
        return hash;
    }

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)_items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();
}
