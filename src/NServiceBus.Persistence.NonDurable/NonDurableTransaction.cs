namespace NServiceBus
{
    using System;
    using System.Collections.Generic;

    class NonDurableTransaction
    {
        public void Enlist(Action action, Action rollbackAction = null)
        {
            actions.Add(action);

            if (rollbackAction != null)
            {
                rollbackActions.Add(rollbackAction);
            }
        }

        public void Commit()
        {
            foreach (var action in actions)
            {
                action();
            }
            actions.Clear();
        }

        public void Rollback()
        {
            foreach (var action in rollbackActions)
            {
                action();
            }

            rollbackActions.Clear();
        }

        List<Action> actions = new List<Action>();
        List<Action> rollbackActions = new List<Action>();
    }
}