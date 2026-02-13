using UnityEngine;
using Photon.Pun;
using UnityEngine.XR.Interaction.Toolkit;

public class NetworkPlayer : MonoBehaviourPun, IPunObservable
{
    [Header("References")]
    [SerializeField] private GameObject cameraRig;
    [SerializeField] private GameObject leftController;
    [SerializeField] private GameObject rightController;

    [Header("Player Visuals")]
    [SerializeField] private GameObject headRepresentation;
    [SerializeField] private GameObject leftHandRepresentation;
    [SerializeField] private GameObject rightHandRepresentation;

    [Header("Player Stats")]
    [SerializeField] private int maxHealth = 100;
    private int currentHealth;
    private int killCount = 0;

    // Networked position tracking for other players
    private Vector3 networkHeadPosition;
    private Quaternion networkHeadRotation;
    private Vector3 networkLeftHandPosition;
    private Quaternion networkLeftHandRotation;
    private Vector3 networkRightHandPosition;
    private Quaternion networkRightHandRotation;

    private void Start()
    {
        currentHealth = maxHealth;

        if (photonView.IsMine)
        {
            // This is our local player
            SetupLocalPlayer();
        }
        else
        {
            // This is a remote player
            SetupRemotePlayer();
        }

        // Set player name
        photonView.Owner.NickName = $"Player_{photonView.Owner.ActorNumber}";
    }

    private void SetupLocalPlayer()
    {
        // Keep XR rig active for local player
        cameraRig.SetActive(true);

        // Hide our own head/hand representations
        if (headRepresentation) headRepresentation.SetActive(false);
        if (leftHandRepresentation) leftHandRepresentation.SetActive(false);
        if (rightHandRepresentation) rightHandRepresentation.SetActive(false);

        // Enable local controllers
        leftController.SetActive(true);
        rightController.SetActive(true);
    }

    private void SetupRemotePlayer()
    {
        // Disable XR rig for remote players
        cameraRig.SetActive(false);

        // Show representations for other players
        if (headRepresentation) headRepresentation.SetActive(true);
        if (leftHandRepresentation) leftHandRepresentation.SetActive(true);
        if (rightHandRepresentation) rightHandRepresentation.SetActive(true);

        // Disable controllers (we'll use representations)
        leftController.SetActive(false);
        rightController.SetActive(false);
    }

    private void Update()
    {
        if (photonView.IsMine)
        {
            // Send our position data
            // Handled in OnPhotonSerializeView
        }
        else
        {
            // Interpolate remote player positions
            UpdateRemotePlayerTransforms();
        }
    }

    private void UpdateRemotePlayerTransforms()
    {
        // Smooth interpolation for remote players
        float lerpSpeed = 10f * Time.deltaTime;

        if (headRepresentation)
        {
            headRepresentation.transform.position = Vector3.Lerp(
                headRepresentation.transform.position,
                networkHeadPosition,
                lerpSpeed
            );
            headRepresentation.transform.rotation = Quaternion.Lerp(
                headRepresentation.transform.rotation,
                networkHeadRotation,
                lerpSpeed
            );
        }

        if (leftHandRepresentation)
        {
            leftHandRepresentation.transform.position = Vector3.Lerp(
                leftHandRepresentation.transform.position,
                networkLeftHandPosition,
                lerpSpeed
            );
            leftHandRepresentation.transform.rotation = Quaternion.Lerp(
                leftHandRepresentation.transform.rotation,
                networkLeftHandRotation,
                lerpSpeed
            );
        }

        if (rightHandRepresentation)
        {
            rightHandRepresentation.transform.position = Vector3.Lerp(
                rightHandRepresentation.transform.position,
                networkRightHandPosition,
                lerpSpeed
            );
            rightHandRepresentation.transform.rotation = Quaternion.Lerp(
                rightHandRepresentation.transform.rotation,
                networkRightHandRotation,
                lerpSpeed
            );
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Send data to network
            // Head
            stream.SendNext(cameraRig.transform.position);
            stream.SendNext(cameraRig.transform.rotation);

            // Left hand
            stream.SendNext(leftController.transform.position);
            stream.SendNext(leftController.transform.rotation);

            // Right hand
            stream.SendNext(rightController.transform.position);
            stream.SendNext(rightController.transform.rotation);

            // Player stats
            stream.SendNext(currentHealth);
            stream.SendNext(killCount);
        }
        else
        {
            // Receive data from network
            networkHeadPosition = (Vector3)stream.ReceiveNext();
            networkHeadRotation = (Quaternion)stream.ReceiveNext();

            networkLeftHandPosition = (Vector3)stream.ReceiveNext();
            networkLeftHandRotation = (Quaternion)stream.ReceiveNext();

            networkRightHandPosition = (Vector3)stream.ReceiveNext();
            networkRightHandRotation = (Quaternion)stream.ReceiveNext();

            currentHealth = (int)stream.ReceiveNext();
            killCount = (int)stream.ReceiveNext();
        }
    }

    [PunRPC]
    public void TakeDamage(int damage, int attackerID)
    {
        if (!photonView.IsMine) return;

        currentHealth -= damage;
        Debug.Log($"Took {damage} damage. Health: {currentHealth}");

        if (currentHealth <= 0)
        {
            Die(attackerID);
        }
    }

    private void Die(int killerID)
    {
        Debug.Log("Player died!");

        // Award kill to attacker
        PhotonView killerView = PhotonView.Find(killerID);
        if (killerView != null)
        {
            killerView.RPC("AddKill", RpcTarget.All);
        }

        // Respawn
        Invoke(nameof(Respawn), 3f);
    }

    [PunRPC]
    private void AddKill()
    {
        killCount++;
        Debug.Log($"Kill count: {killCount}");
    }

    private void Respawn()
    {
        currentHealth = maxHealth;
        // Teleport to spawn point
        transform.position = GetRandomSpawnPoint();
    }

    private Vector3 GetRandomSpawnPoint()
    {
        float radius = 5f;
        float angle = Random.Range(0f, 360f);
        float x = Mathf.Cos(angle * Mathf.Deg2Rad) * radius;
        float z = Mathf.Sin(angle * Mathf.Deg2Rad) * radius;
        return new Vector3(x, 0, z);
    }
}