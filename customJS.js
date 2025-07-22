$(document).ready(function () {
    // Select all rows in the list
    debugger;
    const $button = $(".btn-entitylist-filter-submit");
    $(".float-end").append('<button id="clear" class="btn btn-default" style="margin-left: 10px;">Clear</button>');
    $(".float-end").removeClass("float-end");
    const $Filter1 = $("#0"); 
    const $Filter2 = $("#1"); 
    const $List = $(".table");
    const $clear = $("#clear");
    $clear.on("click", function(event){
        $Filter1.val('');
        $Filter2.val('');
    })
    $List.hide(); // Hide the list
    $(".view-pagination").remove();
    $button.on("click", function(event) {
        debugger;
        if (!$Filter1.val() || !$Filter2.val()) {
            $List.hide(); 
            return;
       } else {
              $List.show(); // Enable the button if both have values
        }
const interval = setInterval(() => {
    debugger;
const rows = document.querySelectorAll("table tbody tr");
// Check if rows exist and if there are more than one
if (rows && rows.length > 1) {
    // Hide all rows except the first one
    rows.forEach((row, index) => {
        if (index !== 0) {
            row.style.display = "none"; // Hides the row
            
        }
    });
    console.log("Extra rows hidden!");

    // Clear the interval once the task is done
    clearInterval(interval);
} else {
    console.log("Waiting for rows to load...");
}

}, 2000); // Check every 500 milliseconds



});
});