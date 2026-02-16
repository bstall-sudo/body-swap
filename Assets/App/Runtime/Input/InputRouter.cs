using UnityEngine;
using App.Runtime.Input;

namespace App.Runtime.Input
{
    public class InputRouter
    {
        public IInputProvider Provider { get; private set; }

        public void SetProvider(IInputProvider provider)
        {
            Provider = provider;
        }
    }
}
