// SPDX-License-Identifier: EUPL-1.2
// Copyright (c) Manuel Josupeit-Walter

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Josupeit.Mail.Milter.Generator;

// An ImmutableArray that compares by its contents.
//
// The incremental pipeline decides whether to re-run the output stage by comparing what the transform
// returned against the previous run's, and ImmutableArray<T>.Equals compares the backing array by
// reference. The transform allocates a fresh array every time, so a record holding one never compares
// equal to itself, RegisterSourceOutput runs on every keystroke, and the generator is incremental in
// name only. MilterContextEmission's own comment says its payload has to be comparable; this is the type
// that makes that true.
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
{
    private readonly ImmutableArray<T> _values;

    public EquatableArray(ImmutableArray<T> values)
    {
        _values = values;
    }

    public int Length => _values.IsDefault ? 0 : _values.Length;

    public static implicit operator EquatableArray<T>(ImmutableArray<T> values)
    {
        return new EquatableArray<T>(values);
    }

    public bool Equals(EquatableArray<T> other)
    {
        if (_values.IsDefault || other._values.IsDefault)
            return _values.IsDefault && other._values.IsDefault;

        return _values.SequenceEqual(other._values, EqualityComparer<T>.Default);
    }

    public override bool Equals(object? obj)
    {
        return obj is EquatableArray<T> other && Equals(other);
    }

    public override int GetHashCode()
    {
        if (_values.IsDefault)
            return 0;

        var hash = 17;

        foreach (var value in _values)
            hash = (hash * 31) + (value?.GetHashCode() ?? 0);

        return hash;
    }

    public IEnumerator<T> GetEnumerator()
    {
        return _values.IsDefault
            ? Enumerable.Empty<T>().GetEnumerator()
            : ((IEnumerable<T>)_values).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
