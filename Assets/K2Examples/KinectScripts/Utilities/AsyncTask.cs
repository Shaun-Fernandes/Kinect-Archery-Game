using System;
using System.Threading;
using UnityEngine;

public enum AsyncTaskState
{
	Running, Failed, Succeed
}

public class AsyncTask<T>
{
    public AsyncTaskState State { get; internal set; }
    public bool LogErrors { get; set; }
    public T Result { get { return _result; } }
	public string ErrorMessage { get { return _errorMessage; } }

    private Func<T> _action;
    private T _result;
	private string _errorMessage;


    public AsyncTask(Func<T> backgroundAction)
    {
        this._action = backgroundAction;
		LogErrors = true;
    }

    public void Start()
    {
        State = AsyncTaskState.Running;
#if !NETFX_CORE
        ThreadPool.QueueUserWorkItem(state => DoInBackground());
#else
		System.Threading.Tasks.Task.Run(() => DoInBackground());
#endif	
    }

    private void DoInBackground()
    {
        _result = default(T);
		_errorMessage = string.Empty;

        try
        {
            if (_action != null)
			{
                _result = _action();
			}

            State = AsyncTaskState.Succeed;
        }
        catch (Exception ex)
        {
            State = AsyncTaskState.Failed;
			_errorMessage = ex.Message;

            if (LogErrors)
			{
                Debug.LogException(ex);
			}
        }
    }
}