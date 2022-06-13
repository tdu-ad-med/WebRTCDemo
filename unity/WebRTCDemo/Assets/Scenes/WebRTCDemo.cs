using System.Collections;
using Unity.WebRTC;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;

class WebRTCDemo : MonoBehaviour
{
#pragma warning disable 0649
    [SerializeField] private InputField url;
    [SerializeField] private Button cameraStart;
    [SerializeField] private Button cameraStop;
    [SerializeField] private Button roomCreate;
    [SerializeField] private Button roomJoin;
    [SerializeField] private Button roomLeave;
    [SerializeField] private Camera cam;
    [SerializeField] private RawImage sourceImage;
    [SerializeField] private RawImage receiveImage;
    [SerializeField] private Transform rotateObject;
#pragma warning restore 0649

    private Coroutine updateCoroutine;

    private void Awake()
    {
        WebRTC.Initialize(EncoderType.Software);
    }

    private void OnDestroy()
    {
        WebRTC.Dispose();
    }

    private void Start()
    {
        url.text = "http://localhost:3000/room0";
        cameraStart.onClick.AddListener(CameraStart);
        cameraStop.onClick.AddListener(CameraStop);
        roomCreate.onClick.AddListener(() => StartCoroutine(create(url.text)));
        roomJoin.onClick.AddListener(() => StartCoroutine(join(url.text)));
        roomLeave.onClick.AddListener(leave);
    }

    private void Update()
    {
        if (rotateObject != null)
        {
            rotateObject.Rotate(1, 2, 3);
        }
    }

    private void CameraStart()
    {
        localStream = cam.CaptureStream(1280, 720, 1000000);
        sourceImage.texture = cam.targetTexture;
        updateCoroutine = StartCoroutine(WebRTC.Update());
        remoteStream = new MediaStream();
        remoteStream.OnAddTrack = e =>
        {
            if (e.Track is VideoStreamTrack track)
            {
                receiveImage.texture = track.InitializeReceiver(1280, 720);
            }
        };
    }

    private void CameraStop()
    {
        StopCoroutine(updateCoroutine);
        updateCoroutine = null;
        localStream.Dispose();
        localStream = null;
        sourceImage.texture = null;
        remoteStream.Dispose();
        remoteStream = null;
        receiveImage.texture = null;
    }

    /* WebRTC */

    private RTCPeerConnection peerConnection;
    public MediaStream localStream, remoteStream;

    public IEnumerator create(string url)
    {
        yield return PostSdp(url, RTCSdpType.Offer, "");  // delete sdp
        createConnection(url);
        var offerOptions = new RTCOfferOptions
        {
            iceRestart = false,
            offerToReceiveAudio = false,
            offerToReceiveVideo = true
        };
        var offer_ = peerConnection.CreateOffer(ref offerOptions);
        yield return offer_;
        var offer = offer_.Desc;
        yield return peerConnection.SetLocalDescription(ref offer);
        while (peerConnection is not null)
        {
            var answerSdp_ = GetSdp(url, RTCSdpType.Answer);
            yield return answerSdp_;
            string answerSdp = (string)answerSdp_.Current;
            if (answerSdp != "")
            {
                var answer = new RTCSessionDescription { type = RTCSdpType.Answer, sdp = answerSdp };
                yield return peerConnection.SetRemoteDescription(ref answer);
                break;
            }
            yield return new WaitForSeconds(1);
        }
    }

    public IEnumerator join(string url)
    {
        createConnection(url);
        while (peerConnection is not null)
        {
            var offerSdp_ = GetSdp(url, RTCSdpType.Offer);
            yield return offerSdp_;
            string offerSdp = (string)offerSdp_.Current;
            if (offerSdp != "")
            {
                var offer = new RTCSessionDescription { type = RTCSdpType.Offer, sdp = offerSdp };
                yield return peerConnection.SetRemoteDescription(ref offer);
                var answerOptions = new RTCAnswerOptions { iceRestart = false };
                var answer_ = peerConnection.CreateAnswer(ref answerOptions);
                yield return answer_;
                var answer = answer_.Desc;
                yield return peerConnection.SetLocalDescription(ref answer);
                break;
            }
            yield return new WaitForSeconds(1);
        }
    }

    public void leave()
    {
        if (peerConnection is not null)
        {
            peerConnection.Close();
            peerConnection = null;
        }
    }

    public void createConnection(string url)
    {
        leave();
        var configuration = new RTCConfiguration { iceServers = new RTCIceServer[0] };
        peerConnection = new RTCPeerConnection(ref configuration);
        peerConnection.OnTrack = e => remoteStream.AddTrack(e.Track);
        peerConnection.OnIceCandidate = e => {
            var desc = peerConnection.LocalDescription;
            StartCoroutine(PostSdp(url, desc.type, desc.sdp));
        };
        foreach (var track in localStream.GetTracks())
        {
            peerConnection.AddTrack(track, localStream);
        }
    }

    /* Http */

    static IEnumerator GetSdp(string url, RTCSdpType type)
    {
        var typeString =
            type == RTCSdpType.Offer ? "offer" :
            type == RTCSdpType.Answer ? "answer" : "";
        var response = HttpGet(url + "/" + typeString);
        yield return response;
        yield return response.Current;
    }

    static IEnumerator PostSdp(string url, RTCSdpType type, string sdp)
    {
        var typeString =
            type == RTCSdpType.Offer ? "offer" :
            type == RTCSdpType.Answer ? "answer" : "";
        var response = HttpPost(url + "/" + typeString, sdp);
        yield return response;
    }

    static IEnumerator HttpGet(string url)
    {
        using (var request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError(request.error);
                yield break;
            }
            yield return request.downloadHandler.text;
        }
    }

    static IEnumerator HttpPost(string url, string body)
    {
        using (var request = UnityWebRequest.Post(url, body))
        {
            request.SetRequestHeader("Content-Type", "text/plain");
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError(request.error);
                yield break;
            }
            yield return null;
        }
    }
}
