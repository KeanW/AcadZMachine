// Global state

var cy;
var layOpts;
var curNode = null;
var curX = 100;
var curY = 100;
var incX = 100;
var incY = 100;

// Shaping layer extension

function sendGraphObjects(jsonArgs) {
  var jsonResponse =
    exec(
      JSON.stringify({
        functionName: 'CreateGraph',
        invokeAsCommand: false,
        functionParams: jsonArgs
      })
    );
  var jsonObj = JSON.parse(jsonResponse);
  if (jsonObj.retCode !== Acad.ErrorStatus.eJsOk) {
    throw Error(jsonObj.retErrorString);
  }
  return jsonObj.result;
}

// Our two callbacks will be registered on page load

function init() {
  registerCallback("setloc", setLocation);
  registerCallback("getgraph", getGraph);
}

// The callbacks themselves

function getGraph(args) {
  var ns =
    cy.nodes().map(
      function (e) {
        var o = {};
        o.data = e.data();
        o.pos = e.position();
        return o;
      });
  var es = cy.edges().map(function (e) { return e.data(); });
  sendGraphObjects({ nodes: ns, edges: es });
}

function setLocation(args) {

  var oargs = JSON.parse(args);

  // Check whether the specified target is already in the graph

  var el = cy.elements("#" + oargs.id);

  var added = false;

  if (oargs.id !== null && curNode !== oargs.id) {

    // If the node isn't in the graph, add it and highlight it

    if (el.length == 0) {

      // Set the position based on the current node
      // (which may have been moved manually)

      if (curNode != null) {
        var cel = cy.elements("#" + curNode);
        if (cel.length > 0) {
          pos = cel[0].position();
          if (pos) {
            curX = pos.x;
            curY = pos.y;
          }
        }
      }

      // If we have a recognised direction, offset the new node position
      // spatially from the previous one

      switch (oargs.dir) {
        case "N":
        case "NORTH":
        case "U":
        case "UP":
          curY -= incY;
          break;
        case "S":
        case "SOUTH":
        case "D":
        case "DOWN":
          curY += incY;
          break;
        case "E":
        case "EAST":
          curX += incX;
          break;
        case "W":
        case "WEST":
          curX -= incX;
          break;
        case "NE":
        case "NORTHEAST":
          curX += incX;
          curY -= incY;
          break;
        case "NW":
        case "NORTHWEST":
          curX -= incX;
          curY -= incY;
          break;
        case "SE":
        case "SOUTHEAST":
          curX += incX;
          curY += incY;
          break;
        case "SW":
        case "SOUTHWEST":
          curX -= incX;
          curY += incY;
          break;
        default:
      }

      // Add the node at the specified location and highlight it

      var node =
        cy.add([
          { group: "nodes", data: oargs, position: { x: curX, y: curY } }
        ]);
      highlightNode(node);

      added = true;
    }
    else {

      // Otherwise - if the node exists in the graph - then highlight it
      // and get its position as the current one

      highlightNode(el[0]);

      pos = el[0].position();
      if (pos) {
        curX = pos.x;
        curY = pos.y;
      }
    }

    // Now we know we have our node in the graph, if we have a previous one,
    // make sure there's an edge connecting them

    if (curNode != null) {

      // Check whether the edge exists

      var edId = curNode + "-" + oargs.id;
      var ed = cy.elements("#" + edId);
      if (ed.length == 0) {

        // If not then add it to the graph

        cy.add([
          {
            group: "edges",
            data:
            {
              id: edId, source: curNode, target: oargs.id, name: oargs.dir,
              dirs: [oargs.dir]
            }
          }
        ]);
      }
      else {

        // If it exists, make sure the direction is added to the list
        // of directions and to the edge's label (the name will have the
        // directions in the order they were found... we could also sort
        // them alphabetically (or whatever) with a bit more work)

        var e = ed[0];
        var d = e.data();
        if (d.dirs.indexOf(oargs.dir) < 0) {
          d.dirs.push(oargs.dir);
          e.data("name", d.name + "," + oargs.dir);
        }
      }
    }

    // We have a new current node

    curNode = oargs.id;
  }

  // Update the layout if a node has been added
  // (no need to do this when an edge has been added)

  if (added) {
    cy.layout(layOpts);
  }
}

// Helper function to highlight a node via CSS

function highlightNode(node) {
  cy.nodes('*').removeClass('highlighted');
  node.addClass('highlighted');
}

$(function () { // on DOM ready

  layOpts = {

    name: 'cola',
    animate: true, // whether to show the layout as it's running
    refresh: 1, // number of ticks per frame; higher is faster but more jerky
    maxSimulationTime: 4000, // max length in ms to run the layout
    ungrabifyWhileSimulating: false, // so you can't drag nodes during layout
    fit: true, // on every layout reposition of nodes, fit the viewport
    padding: 30, // padding around the simulation
    boundingBox: undefined, // constrain layout bounds; { x1, y1, x2, y2 } or { x1, y1, w, h }

    // layout event callbacks
    ready: function () { }, // on layoutready
    stop: function () { }, // on layoutstop

    // positioning options
    randomize: true, // use random node positions at beginning of layout
    avoidOverlap: true, // if true, prevents overlap of node bounding boxes
    handleDisconnected: true, // if true, avoids disconnected components from overlapping
    nodeSpacing: function (node) { return 10; }, // extra spacing around nodes
    flow: undefined, // use DAG/tree flow layout if specified, e.g. { axis: 'y', minSeparation: 30 }
    alignment: undefined, // relative alignment constraints on nodes, e.g. function( node ){ return { x: 0, y: 1 } }

    // different methods of specifying edge length
    // each can be a constant numerical value or a function like `function( edge ){ return 2; }`
    edgeLength: undefined, // sets edge length directly in simulation
    edgeSymDiffLength: undefined, // symmetric diff edge length in simulation
    edgeJaccardLength: undefined, // jaccard edge length in simulation

    // iterations of cola algorithm; uses default values on undefined
    unconstrIter: undefined, // unconstrained initial layout iterations
    userConstIter: undefined, // initial layout iterations with user-specified constraints
    allConstIter: undefined, // initial layout iterations with all constraints including non-overlap

    // infinite layout options
    infinite: false // overrides all other options for a forces-all-the-time mode
  }

  cy = cytoscape({
    container: document.getElementById('cy'),

    boxSelectionEnabled: false,
    autounselectify: true,

    style: cytoscape.stylesheet()
      .selector('node')
        .css({
          'content': 'data(name)',
          'text-valign': 'center',
          'color': 'white',
          'text-outline-width': 2,
          'text-outline-color': '#888'
        })
      .selector('edge')
        .css({
          'content': 'data(name)',
          'color': '#888',
          'target-arrow-shape': 'triangle',
          'width': 4,
          'line-color': '#ddd',
          'target-arrow-color': '#ddd'
        })
      .selector('.highlighted')
        .css({
          'background-color': '#f00',
          'line-color': '#f00',
          'target-arrow-color': '#f00',
          'transition-property':
             'background-color, line-color, target-arrow-color',
          'transition-duration': '0.5s'
        })
      .selector(':selected')
        .css({
          'background-color': 'black',
          'line-color': 'black',
          'target-arrow-color': 'black',
          'source-arrow-color': 'black'
        })
      .selector('.faded')
        .css({
          'opacity': 0.25,
          'text-opacity': 0
        }),

    elements: { nodes: [], edges: [] },

    layout: layOpts
  });

}); // on DOM ready
