public enum PropagationBehavior
{
    Local,           // Stay within current scope
    UpToParent,      // Also propagate to parent scope
    DownToChildren,  // Also propagate to child scopes
    UpAndDown        // Propagate both ways
}