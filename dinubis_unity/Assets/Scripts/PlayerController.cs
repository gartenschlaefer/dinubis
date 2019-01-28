using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using System.Linq;

public class PlayerController : NetworkBehaviour
{
  // public
  public int jump_limit = 4;
  public float walkSpeed = 1;
  public float runSpeed = 3;
  public float turnSmoothTime = 0.2f;
  public float speedSmoothTime = 0.1f;
  public float gravity = -12;
  public float jumpHeight = 0.5f;



  public float attackDamage = 1f;
  private float maxDistance = 10f;
  private float maxDistanceResi = 75f;
  private Resource resource;
  private GameObject[] resources;
  private GameObject[] resis;
  private OSC myOsc;
  //private float distance_resi;
  //private float distance_resource;




  //controls how much the player can turn while in mid air
  public float inAirControl = 1;          
  public bool move_activated = true;

  // private
  private bool walk = false;
  private bool run = false;
  private int jump_counter;

  private float turnSmoothVelocity;
  private float speedSmoothVelocity;
  private float currentSpeed;
  private float velocityY;

  private Animator anim;
  private Transform cameraT;
  private CharacterController controller;


  // start
  void Start()
  {
    anim = GetComponentInChildren<Animator>();
    cameraT = Camera.main.transform;
    controller = GetComponent<CharacterController>();
    myOsc = GameObject.Find ("OSCManager").GetComponent<OSC> ();
    jump_counter = 0;

  }

  // Move Character
  public void Move(Vector2 input, bool run_active)
  {
    if (!isLocalPlayer) {
      return;
    };

    // control movement
    Vector2 inputDirection = input.normalized;
    float targetRotation = Mathf.Atan2(inputDirection.x, inputDirection.y) * Mathf.Rad2Deg + cameraT.eulerAngles.y;
    transform.eulerAngles = Vector3.up * Mathf.SmoothDampAngle(transform.eulerAngles.y, targetRotation, ref turnSmoothVelocity, GetModifiedSmoothTime(turnSmoothTime));

    if (controller.enabled)
    {
      float targetSpeed = (run_active ? runSpeed : walkSpeed) * inputDirection.magnitude;
      currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed, ref speedSmoothVelocity, GetModifiedSmoothTime(speedSmoothTime));

      velocityY += Time.deltaTime * gravity;
      Vector3 velocity = transform.forward * currentSpeed + Vector3.up * velocityY;
      controller.Move(velocity * Time.deltaTime);
      currentSpeed = new Vector2(controller.velocity.x, controller.velocity.z).magnitude;
      if (controller.isGrounded){
        velocityY = 0;
      }
      //this is how we tell the animation controller which state we're in
      float animationSpeedPercent = (run ? currentSpeed / runSpeed : currentSpeed / walkSpeed * 0.5f);
      
      // animation
      anim.SetFloat("speed", animationSpeedPercent, GetModifiedSmoothTime(speedSmoothTime), Time.deltaTime);

      // walk and run sound
      if (animationSpeedPercent > 0.1 && animationSpeedPercent < 0.6 && !walk){

        if (!walk)
        OSCPlayerWalk();

        if (run)
        OSCPlayerRunStop();

        walk = true;
        run = false;

      }
      else if (animationSpeedPercent > 0.6 && !run){

        if (walk)
        OSCPlayerWalkStop();

        if (!run)
        OSCPlayerRun();
        
        run = true;
        walk = false;

      }
      else if (animationSpeedPercent < 0.1 && (walk || run)){
        if (run)
            OSCPlayerWalkStop();

        if (walk)
            OSCPlayerRunStop();

        walk = false;
        run = false;
        
      
      }
    }
  }

 [Command]
  public void CmdDig()  //in InputHandler
  {
    if (!isLocalPlayer) {
      return;
    };

    // Find objects
    resources = GameObject.FindGameObjectsWithTag("Resource");
    resis = GameObject.FindGameObjectsWithTag("Resident");

    //Calculate distances
    Dictionary<GameObject, float> resi_dict = new Dictionary<GameObject, float>();
    Dictionary<GameObject, float> resource_dict = new Dictionary<GameObject, float>();


    foreach (GameObject resi in resis)
    { 
      Vector3 player_pos = gameObject.GetComponent<Transform>().position;
      Vector3 resi_pos = resi.GetComponent<Transform>().position;
      float distance_resi = (player_pos - resi_pos).sqrMagnitude;
      resi_dict.Add(resi, distance_resi);

    }
    
    foreach (GameObject resource in resources)
    {
        Vector3 player_pos = gameObject.GetComponent<Transform>().position;
        Vector3 resource_pos = resource.GetComponent<Transform>().position;
        float distance_resource = (player_pos - resource_pos).sqrMagnitude;
        resource_dict.Add(resource, distance_resource);
        //Debug.Log("distance_resource: "+distance_resource);
    }

    float min_resi_dist = resi_dict.Values.Min();   //resi_dict.distance_resi.Min();
    float min_resource_dist = resource_dict.Values.Min(); //resource_dict.distance_resource.Min();

    if (min_resource_dist < maxDistance) {  //distance_resource
      if (min_resi_dist > maxDistanceResi) {   //distance_resi
        GameObject closestResource = FindClosestResource(resource_dict);
        

      //   Debug.Log("Damaged");
      //           if (isServer)
      //           {
      //               RpcDamage(attackDamage);
      //           }
      //           else
      //           {
      //               CmdDamage(attackDamage);
      //           }
      // }

        closestResource.GetComponent<Resource>().TakeDamage(attackDamage);
      }
    }
  }



 private GameObject FindClosestResource(Dictionary<GameObject, float> resource_dict)
  {
    //var ordered = nubi_dict.OrderBy(x => x.Value);
    float min_value = resource_dict.Values.Min();
    var closest_resources = resource_dict.Where(resource => resource.Value.Equals(min_value)).Select(resource => resource.Key);
    foreach (GameObject closest_resource in closest_resources){
      return closest_resource;
    }
    return null;
  }





  //OSC Messages

  //Player Walk
  private void OSCPlayerWalk(){
  OscMessage msg = new OscMessage ();
  msg.address = "/player_walk";
  msg.values.Add (1);
  //msg.values.Add (transform.position.x);
  myOsc.Send (msg);
  //Debug.Log("Send OSC message /player_walk");
  }

  //Player Run
  private void OSCPlayerRun(){
  OscMessage msg = new OscMessage ();
  msg.address = "/player_run";
  msg.values.Add (1);
  //msg.values.Add (transform.position.x);
  myOsc.Send (msg);
  //Debug.Log("Send OSC message /player_run");
  }

  //Player Stop
  private void OSCPlayerWalkStop(){
  OscMessage msg = new OscMessage ();
  msg.address = "/player_walk";
  msg.values.Add (0);
  //msg.values.Add (transform.position.x);
  myOsc.Send (msg);
  //Debug.Log("Send OSC message /player_stop");
  }


  private void OSCPlayerRunStop(){
  OscMessage msg = new OscMessage ();
  msg.address = "/player_run";
  msg.values.Add (0);
  //msg.values.Add (transform.position.x);
  myOsc.Send (msg);
  //Debug.Log("Send OSC message /player_stop");
  }


  //--


  // play the flute
  public void GetAttacked()
  {
    //anim.SetTrigger("getAttacked");
    //sound_player.hamlin_hurt.Play();
  }

  //should work even when movement disabled
  public void Jump()
  {
    if (!isLocalPlayer) {
      return;
    };
    if (controller.isGrounded)
    {
      jump_counter = 0;
    }
    if (jump_counter < jump_limit)
    {
      float jumpVelocity = Mathf.Sqrt(-2 * gravity * jumpHeight);
      velocityY = jumpVelocity;
      jump_counter += 1;
    }
  }

  //not convinced this is really that useful - may delete later
  float GetModifiedSmoothTime(float smoothTime)
  {
    if (controller.isGrounded) return smoothTime;
    else if (inAirControl == 0) return float.MaxValue;
    else return (smoothTime / inAirControl);
  }
}