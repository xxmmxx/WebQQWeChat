﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HttpActionTools.Event;

namespace HttpActionTools.Action
{
    public class ActionLink : IActionLink, IActionEventHandler
    {
        private readonly ManualResetEvent _waitHandle = new ManualResetEvent(false);
        private readonly object _syncObj = new object();
        private bool _isWaiting;
        private ActionEvent _finalEvent;
        private readonly ActionEventListener _listener;
        private CancellationTokenSource _cts;

        public ActionLink(ActionEventListener listener)
        {
            _cts = new CancellationTokenSource();
            _listener = listener;
            OnActionEvent += listener;
        }

        public void Cancel()
        {
            _cts.Cancel();
        }

        public CancellationToken Token => _cts.Token;

        public void Terminate(IAction sender, ActionEvent actionEvent)
        {
            _finalEvent = actionEvent;
            OnActionEvent?.Invoke(sender, actionEvent);
            _waitHandle.Set();
        }

        public ActionEvent WaitFinalEvent()
        {
            return WaitFinalEvent(CancellationToken.None);
        }

        public ActionEvent WaitFinalEvent(int second)
        {
            _cts.CancelAfter(second * 1000);
            return WaitFinalEvent(_cts.Token);
        }

        public ActionEvent WaitFinalEvent(CancellationToken token)
        {
            var ex = new Exception("this action is still waiting...");
            if (_isWaiting) throw ex;
            lock (_syncObj)
            {
                if (_isWaiting) throw ex;
                if (_cts.Token != token) _cts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, token);
                _isWaiting = true;
                _waitHandle.WaitOne();
                _isWaiting = false;
                return _finalEvent;
            }
        }

        public Task<ActionEvent> WaitFinalEventAsync()
        {
            return Task.Run(() => WaitFinalEvent(), CancellationToken.None);
        }

        public Task<ActionEvent> WaitFinalEventAsync(int second)
        {
            var token = new CancellationTokenSource(second * 1000).Token;
            return WaitFinalEventAsync(token);
        }

        public Task<ActionEvent> WaitFinalEventAsync(CancellationToken token)
        {
            return Task.Run(() => WaitFinalEvent(token), token);
        }

        protected virtual bool IsFinalEvent(ActionEvent Event)
        {
            var type = Event.Type;
            return type == ActionEventType.EvtCanceled
                    || type == ActionEventType.EvtError
                    || type == ActionEventType.EvtOK;
        }

        public event ActionEventListener OnActionEvent;
    }
}
