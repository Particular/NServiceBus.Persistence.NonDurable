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
            if (transactionIsCommitted)
            {
                throw new InvalidOperationException("The transaction has already been committed.");
            }

            foreach (var action in actions)
            {
                action();
            }

            transactionIsCommitted = true;
        }

        public void Rollback()
        {
            foreach (var action in rollbackActions)
            {
                action();
            }
        }

        bool transactionIsCommitted = false;
        List<Action> actions = [];
        List<Action> rollbackActions = [];
    }
}
