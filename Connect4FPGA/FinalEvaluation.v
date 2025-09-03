/*
This module ties together the SPI and Evaluation modules in
such a way that the raspberry pi data will be received and
assessed so that a proper evaluation might be sent back.
*/


module FinalEvaluation(
	input clk, //fpga clk
	input master_clk, //clk from rpi
	input cs, //cs line from pi
	input final_evaluation_reset, //resets the finalevaluation instance
	input d_in, //serial data sent by pi
	output d_out, //used to serially send out the final evaluation
	output out_enable //flag that if 1 enables spi to send out final evaluation
);

	
	//spi related
	reg receive_or_send_data; //flag that if 1 prepares spi to receive data and if 0 to send data
	reg spi_reset; //resets the spi instance
	wire [7:0] is_max_or_min; //flag that if 1 returns the max of the evaluations and if 0 returns the min
	wire [7:0] batch_size; //number of boards in the batch
	wire done_sending; //flag that if 1 signals that spi is done sending the final evaluation back to the rpi
	reg final_evaluation_stable; //determines whether the final evaluation is stable
	reg signed [31:0] final_evaluation; //storage for the final evaluation
	
	//evaluation related
	reg evaluation_reset; //resets the evaluation instances
	wire [6:0] evaluation_stable; //determines whether each board's evaluation is finished
	reg [6:0] evaluation_enable; //enables or keeps disabled evaluation instances depending on number of boards (max of 7)
	wire signed [31:0] evaluations [6:0]; //board evaluations for each board in the batch with a max of 7 evaluations
	reg signed [31:0] latched_evaluations [6:0];
	
	//used by everything
	wire rpi_data_stable; //flag that if 1 signals input from rpi is stable
	wire [615:0] batch; //batch of connect4 boards with max of 7 boards
	
	//just for finalevaluation module
	integer j; //for-loop interator that enables batch_size number of evaluation instances
	reg signed [31:0] max_eval; //temporary storage for the current max or min evaluation
	reg [2:0] calc_counter;
	
	//state related
	(* preserve, noprune *) reg [2:0] state;
	localparam IDLE = 3'd0;
	localparam ENABLE = 3'd1;
	localparam LATCH = 3'd2;
	localparam CALC = 3'd3;
	localparam SEND = 3'd4;
	localparam DONE = 3'd5;

	always @(posedge clk) begin
		//resets everything to a known state
		//resets the spi and evaluation instances as well
		if (!final_evaluation_reset) begin
			  spi_reset <= 0;
			  state <= IDLE;
			  final_evaluation <= 0;
			  receive_or_send_data <= 1;
			  evaluation_enable <= 0;
			  final_evaluation_stable <= 0;
			  evaluation_reset <= 0;
			  calc_counter <= 0;
			  for (j = 0; j < 7; j = j + 1) begin
				  latched_evaluations[j] <= 0;
			  end

		
		end
		else begin
			case (state)
				IDLE: begin
					//activate SPI and evaluation logic
					spi_reset <= 1;
					evaluation_reset <= 1;

					//prepares to receive data from rpi
					receive_or_send_data <= 1;

					//reset control flags
					final_evaluation_stable <= 0;
					evaluation_enable <= 0;
					final_evaluation <= 0;
					
					calc_counter <= 0;
					for (j = 0; j < 7; j = j + 1) begin
						latched_evaluations[j] <= 0;
				   end

					//transition to ENABLE when rpi pulls CS high
					if (cs == 1)
						state <= ENABLE;
				end
				
				//enables the needed amount of evaluation instances
				ENABLE: begin
					if (rpi_data_stable == 1) begin
						for (j = 0; j < batch_size[2:0]; j = j + 1) begin
							if (j < 7) evaluation_enable[j] <= 1;
						end
						state <= LATCH;
					end

				end
				
				LATCH: begin
					if ((evaluation_stable & evaluation_enable) == evaluation_enable) begin
						for (j = 0; j < 7; j = j + 1) begin
							if (evaluation_enable[j]) begin
								latched_evaluations[j] <= evaluations[j];
							end
						end
						state <= CALC;
					end
				end

				
				//determines the max or min evaluation or the value of max_or_min
				CALC: begin
					if (calc_counter == 0) begin
						// Initialize with first active evaluation
						max_eval = latched_evaluations[0];
						calc_counter <= calc_counter + 1;
					end else begin
						if (evaluation_enable[calc_counter]) begin
							if ((is_max_or_min && latched_evaluations[calc_counter] > max_eval) ||
								(!is_max_or_min && latched_evaluations[calc_counter] < max_eval)) begin
								max_eval = latched_evaluations[calc_counter];
							end
						end
						if (calc_counter == 6) begin
							final_evaluation <= max_eval;
							final_evaluation_stable <= 1;
							state <= SEND;
						end else begin
							calc_counter <= calc_counter + 1;
						end
					end
				end
				
				//continues through state machine when finished sending the final evaluation
				SEND: begin
					receive_or_send_data <= 0;
					if (done_sending) state <= DONE;
					
				end
				
				DONE: begin
					state <= IDLE;
				
				end
				
				default:
					state <= IDLE;
				
			endcase
		end
		
	end

	//spi instance
	SPI spi(
		.clk(clk),
		.master_clk(master_clk),
		.cs(cs),
		.d_in(d_in),
		.receive_or_send_data(receive_or_send_data),
		.reset(spi_reset),
		.evaluation_stable(final_evaluation_stable),
		.evaluation(final_evaluation),
		.d_out(d_out),
		.out_enable(out_enable),
		.rpi_data_stable(rpi_data_stable),
		.is_max_or_min(is_max_or_min),
		.batch_size(batch_size),
		.batch(batch),
		.done_sending(done_sending)
	);

	//generates the maximum needed evaluation instances
	genvar i;
	generate
		for (i = 0; i < 7; i = i + 1) begin : total_evaluations
			Evaluation eval_instance(
				.clk(clk),
				.reset(evaluation_reset),
				.rpi_data_stable(rpi_data_stable),
				.board(batch[(88 * i) +: 84]),
				.enable(evaluation_enable[i]),
				.evaluation_stable(evaluation_stable[i]),
				.evaluation(evaluations[i])
				);
		end
	endgenerate

endmodule