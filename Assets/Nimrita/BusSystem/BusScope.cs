using System;
using System.Collections.Generic;
using System.Linq;

public class BusScope
{
    // Predefined scopes
    public static readonly BusScope Global = new BusScope("Global", null);
    public static readonly BusScope Core = new BusScope("Core", Global);
    public static readonly BusScope Networking = new BusScope("Networking", Core);
    public static readonly BusScope UI = new BusScope("UI", Core);

    public string Name { get; }
    public BusScope Parent { get; }
    private readonly HashSet<BusScope> _children = new HashSet<BusScope>();

    private BusScope(string name, BusScope parent)
    {
        Name = name;
        Parent = parent;

        if (parent != null)
            parent._children.Add(this);
    }

    // Create custom scopes at runtime if needed
    public BusScope CreateChildScope(string name)
    {
        return new BusScope(name, this);
    }

    public bool IsWithinHierarchy(BusScope scope)
    {
        if (this == scope) return true;
        if (Parent == null) return false;
        return Parent.IsWithinHierarchy(scope);
    }

    public IEnumerable<BusScope> GetChildScopes(bool recursive = false)
    {
        foreach (var child in _children.ToList())
        {
            yield return child;

            if (recursive)
            {
                foreach (var grandchild in child.GetChildScopes(true))
                    yield return grandchild;
            }
        }
    }

    public override string ToString()
    {
        return Name;
    }
}