//Copyright 2013 MichaelTaylor3D
//www.michaeltaylor3d.com

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Threading;


public delegate void FailScript();
public delegate void DownloadCallback(WWW www);


public class DownloadDoOnMainThread : MonoBehaviour {
	
	public readonly static Queue<Action> ExecuteOnMainThread = new Queue<Action>();
	
	public virtual void Update()
	{
		// dispatch stuff on main thread
		while (ExecuteOnMainThread.Count > 0)
		{
			ExecuteOnMainThread.Dequeue().Invoke();
		}
	}
}

public class Download : MonoBehaviour
{

    /////////////////////////////////////////////////
    //////-------------Public API--------------//////
    /////////////////////////////////////////////////

    /// <summary>
    /// Clears the download queue.
    /// </summary>
    public static void ClearQueue()
    {
        _downloadQueue.Clear();
    }

    /// <summary>
    ///  Downloads a file Async and sends it to the callback.
    /// </summary>
    public static void Async(string path, DownloadCallback callback)
    {
        Job job = new Job(path, callback, false, 0, null);
        _singleton.Enqueue(job);
    }

    /// <summary>
    ///  Downloads a file Async and sends it to the callback.
    ///  Invokes the failscript if the download failed
    /// </summary>
    public static void Async(string path, DownloadCallback callback, FailScript failscript)
    {
        Job job = new Job(path, callback, false, 0, failscript);
        _singleton.Enqueue(job);
    }

    /// <summary>
    ///  Downloads a file Async and sends it to the callback.
    ///  caches image files to local disk
    ///  retries the failed download a set number of times
    /// </summary>
    public static void Async(string path, DownloadCallback callback, bool saveToLocal, int downloadRetries)
    {
        Job job = new Job(path, callback, saveToLocal, downloadRetries, null);
        _singleton.Enqueue(job);
    }

	/// <summary>
	///  Downloads a file Async and sends it to the callback.
	///  caches image files to local disk
	///  retries the failed download a set number of times
	///  Invokes the failscript if the download failed
	/// </summary>
	public static void Async(string path, DownloadCallback callback, FailScript failscript, bool saveToLocal, int downloadRetries )
	{
		Job job = new Job(path, callback, saveToLocal, downloadRetries, failscript);
		_singleton.Enqueue(job);
	}

    /// <summary>
    ///  Downloads a file Async and sends it to the callback.
    ///  retries the failed download a set number of times
    /// </summary>
    public static void Async(string path, DownloadCallback callback, int downloadRetries)
    {
        Async(path, callback, false, downloadRetries);
    }

    /// <summary>
    ///  Downloads a file Async and sends it to the callback.
    ///  caches image files to local disk
    /// </summary>
    public static void Async(string path, DownloadCallback callback, bool saveToLocal)
    {
        Async(path, callback, saveToLocal, 0);
    }

    /// <summary>
    /// Downloads a file in Sync and returns the result
    /// Useful for getting RESTful callbacks, NOT Images
    /// </summary>
    public static string Sync(string path)
    {
        string result;
        new Download.HTTP(path, out result);
        return result;
    }

    /// <summary>
    /// Gets a value indicating whether the download queue is active.
    /// </summary>
    /// <value>
    /// <c>true</c> if this download queue is active; otherwise, <c>false</c>.
    /// </value>	
    public static bool IsActive
    {
        get
        {
            return _IsActive;
        }
    }
    private static bool _IsActive;

    /////////////////////////////////////////////////
    //////-------------Queue Object--------------//////
    /////////////////////////////////////////////////
    public sealed class Job
    {
        public string path { get; set; }
        public DownloadCallback callback { get; set; }
        public bool saveToLocal { get; set; }
        public int downloadRetries { get; set; }
        public FailScript failscript;
		public string filePath;
		public WWW www;

		private byte[] bytes;

        public Job(string path, DownloadCallback callback, bool saveToLocal, int downloadRetries, FailScript failscript)
        {
            this.path = path;
            this.callback = callback;
            this.saveToLocal = saveToLocal;
            this.failscript = failscript;
			this.filePath = null;
			this.www = null;

            if (downloadRetries != default(int))
            {
                this.downloadRetries = downloadRetries;
            }
            else
            {
                this.downloadRetries = 0;
            }
        }

		public void Persist(){
			if(this.saveToLocal && this.filePath!=null){
				Thread thread = new Thread(SaveData);
				bytes = www.bytes;
				thread.Start();
			}
			else{
				if (this.callback != null)
				{
					this.callback(www);
				}
			}
		}

		private void SaveData() {
			if (!Directory.Exists(downloadDirectory))
			{
				Directory.CreateDirectory(downloadDirectory);
			}

			//iOS
			if (Application.platform == RuntimePlatform.IPhonePlayer){
				#if UNITY_IPHONE
				UnityEngine.iOS.Device.SetNoBackupFlag(this.filePath); //Apple will reject the app if this is backed up
				#endif
			}

			File.WriteAllBytes(this.filePath, this.bytes);
			this.filePath = null;
			if (this.callback != null)
			{
				DownloadDoOnMainThread.ExecuteOnMainThread.Enqueue(() => {  
					this.callback(www);
				});
			}		
		}

    }

	/////////////////////////////////////////////////
	//////------------I/O Support--------------//////
	/////////////////////////////////////////////////
	public sealed class ImageToSave
	{
		public string fileName { get; set; }
		public byte[] bytes { get; set; }

		public ImageToSave(string fileName, byte[] bytes)
		{
			this.fileName = fileName;
			this.bytes = bytes;
		}
	}


    /////////////////////////////////////////////////
    //////---------Instance Members------------//////
    /////////////////////////////////////////////////

    #region Singleton
	private static DownloadDoOnMainThread _instanceDownloadDoOnMainThread;
	private static Download _instance;
	private static Download _singleton
    {
        get
        {
            if (_instance == null)
            {
				GameObject runCode = GameObject.Find("RunCode");

				if (runCode == null)
                {
                    runCode = new GameObject("RunCode");
                }

				_instance = runCode.AddComponent(typeof(Download)) as Download;
			}

			if(_instanceDownloadDoOnMainThread == null){
				GameObject downloadDoOnMainThread = GameObject.Find("DownloadDoOnMainThread");

				if (downloadDoOnMainThread == null)
				{
					downloadDoOnMainThread = new GameObject("DownloadDoOnMainThread");
				}

				_instanceDownloadDoOnMainThread = downloadDoOnMainThread.AddComponent(typeof(DownloadDoOnMainThread)) as DownloadDoOnMainThread;
			}
								
			return _instance;
        }
    }
    #endregion

    private static Queue<Job> _downloadQueue;
    private static string downloadDirectory;

    void Awake()
    {
        _IsActive = false;
        _jobIsProcessing = false;
        _downloadQueue = new Queue<Job>();

		if (Application.platform == RuntimePlatform.IPhonePlayer){
			string path = Application.persistentDataPath.Substring( 0, Application.persistentDataPath.Length - 5 );
			path = path.Substring( 0, path.LastIndexOf( '/' ) );
			downloadDirectory =  Path.Combine( path, "Documents/downloads/" );
		}
		else{
			downloadDirectory = Application.persistentDataPath + "/downloads/";
		}

		//Debug.Log("DOWNLOAD - downloadDirectory: "+downloadDirectory);

    }
	
    void FixedUpdate()
    {
        if (_downloadQueue.Count == 0)
        {
            _IsActive = false;
            return;
        }

        _IsActive = true; 

        if (!_jobIsProcessing
        && Internet.isConnected())
        {
            StartCoroutine(ProcessJob());
        }
    }

    private static bool _jobIsProcessing;

    private static IEnumerator ProcessJob()
    {
        _jobIsProcessing = true;

        Job job = _downloadQueue.Dequeue() as Job;

        if (job != null)
        {
			string fileName = Path.GetFileNameWithoutExtension(job.path.Replace("%20", " "));
			string fileExtension = Path.GetExtension(job.path);
			string filePath = downloadDirectory + fileName + fileExtension;
			job.filePath = filePath.Replace("%20", " ");

			WWW www = null;

            for (int i = 0; i <= job.downloadRetries; i++)
			{
				if (job.saveToLocal && File.Exists(filePath)){
					www = new WWW("file://" + job.filePath);
				}
				else{
					www = new WWW(job.path);
				}
				yield return www;
				if (www.error != null)
				{
					www.Dispose();
					www = null;
					continue;
				}
				job.www = www;
                break;
            }

			if(job.www != null){
				job.Persist();
			}
			else{
//				Debug.LogError("Download Error: " + www.url + " : " + www.error);
				if (job.failscript != null){
					job.failscript();
				}
			}

        }
		_jobIsProcessing = false;
    }
	
    private void Enqueue(Job job)
    {
        _downloadQueue.Enqueue(job);
    }

    ///////////////////////////////////////////////////////////////////
    /////////////////////Sync Downloading//////////////////////////////
    ////// Should Be used in editor only: Freezes IOS devices//////////

    private class HTTP
    {
        public HTTP(string request, out string result)
        {
            result = ServerRequest(request);
        }

        private IEnumerator DownloadString(string url)
        {
            WWW www = new WWW(url);
            float wwwStartTime = Time.realtimeSinceStartup;
            float wwwTimeOutSec = 50f;

            while (!www.isDone)
            {
                if (Time.realtimeSinceStartup - wwwStartTime > wwwTimeOutSec)
                {
                    Debug.LogError("Download time Out");
                    //yield return www.text;
                }
            }

            yield return www.text;
        }

        private string ServerRequest(string request)
        {
            string result = null;
            IEnumerator e;

            //Send a RESTful server Request
            e = DownloadString(request);

            //get the last result of the enumeration
            while (e.MoveNext())
            {
                result = e.Current.ToString();
            }

            //check for server response errors
            if (isServerResponseError(result))
            {
                Debug.LogError(result);
                return null;
            }
            return result;
        }

        private bool isServerResponseError(string www)
        {
            return www.Contains("Error:") ? true : false;
        }
    }
}

public static class Internet
{

    public static bool isConnected()
    {
        return (Network.player.ipAddress.ToString() == "127.0.0.1") ? false : true;
    }
}
